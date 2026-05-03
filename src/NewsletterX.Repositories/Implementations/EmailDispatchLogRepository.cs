namespace NewsletterX.Repositories.Implementations;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using NewsletterX.EntityModels.DynamoDB;
using NewsletterX.Repositories.Interfaces;
using NewsletterX.Types.Enums;

/// <summary>
/// DynamoDB implementation of IEmailDispatchLogRepository.
/// </summary>
/// <remarks>
/// DYNAMODB IMPLEMENTATION PATTERNS:
/// ─────────────────────────────────
/// 1. Uses low-level API (IAmazonDynamoDB) for fine-grained control
/// 2. DynamoDBContext is also available for simpler CRUD (trade-off: less control)
/// 3. Batch operations respect DynamoDB limits (25 items per batch)
/// 4. Conditional writes used for idempotency where applicable
/// 
/// CLIENT REUSE (CRITICAL):
/// - IAmazonDynamoDB MUST be a singleton
/// - It manages its own HTTP connection pool
/// - Creating per-request = socket exhaustion under load
/// 
/// ERROR HANDLING:
/// - ConditionalCheckFailedException: Expected when duplicate write prevented
/// - ProvisionedThroughputExceededException: Need to retry with backoff
/// - SDK has built-in retry logic; configure via AmazonDynamoDBConfig
/// </remarks>
public class EmailDispatchLogRepository : IEmailDispatchLogRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly DynamoDBContext _context;
    private readonly ILogger<EmailDispatchLogRepository> _logger;
    private const string TableName = "EmailDispatchLog";
    private const int MaxBatchSize = 25; // DynamoDB limit

    public EmailDispatchLogRepository(
        IAmazonDynamoDB dynamoDb,
        ILogger<EmailDispatchLogRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _context = new DynamoDBContext(dynamoDb);
        _logger = logger;
    }

    public async Task LogDispatchAsync(
        EmailDispatchLogEntity entity, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use conditional put to prevent duplicates (idempotency)
            var request = new PutItemRequest
            {
                TableName = TableName,
                Item = EntityToAttributes(entity),
                // Only write if the item doesn't already exist
                ConditionExpression = "attribute_not_exists(NewsletterType) AND attribute_not_exists(DispatchTimestamp)"
            };

            await _dynamoDb.PutItemAsync(request, cancellationToken);
            
            _logger.LogDebug(
                "Logged dispatch: {NewsletterType}/{DispatchTimestamp}", 
                entity.NewsletterType, 
                entity.DispatchTimestamp);
        }
        catch (ConditionalCheckFailedException)
        {
            // Item already exists - this is expected for idempotent retries
            _logger.LogDebug(
                "Dispatch log already exists (idempotent): {NewsletterType}/{DispatchTimestamp}", 
                entity.NewsletterType, 
                entity.DispatchTimestamp);
        }
    }

    public async Task LogDispatchBatchAsync(
        IEnumerable<EmailDispatchLogEntity> entities, 
        CancellationToken cancellationToken = default)
    {
        var items = entities.ToList();
        
        // Chunk into batches of 25 (DynamoDB limit)
        var batches = items
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / MaxBatchSize)
            .Select(g => g.Select(x => x.item).ToList());

        foreach (var batch in batches)
        {
            var writeRequests = batch.Select(entity => new WriteRequest
            {
                PutRequest = new PutRequest
                {
                    Item = EntityToAttributes(entity)
                }
            }).ToList();

            var request = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    { TableName, writeRequests }
                }
            };

            // Handle unprocessed items (can occur under throttling)
            var response = await _dynamoDb.BatchWriteItemAsync(request, cancellationToken);
            
            // Retry unprocessed items with exponential backoff
            var unprocessed = response.UnprocessedItems;
            var retryCount = 0;
            const int maxRetries = 3;

            while (unprocessed.Count > 0 && retryCount < maxRetries)
            {
                retryCount++;
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, retryCount) * 100);
                
                _logger.LogWarning(
                    "Batch write had {Count} unprocessed items, retrying in {Delay}ms (attempt {Retry}/{Max})",
                    unprocessed.Values.Sum(v => v.Count),
                    delay.TotalMilliseconds,
                    retryCount,
                    maxRetries);

                await Task.Delay(delay, cancellationToken);
                
                var retryRequest = new BatchWriteItemRequest { RequestItems = unprocessed };
                response = await _dynamoDb.BatchWriteItemAsync(retryRequest, cancellationToken);
                unprocessed = response.UnprocessedItems;
            }

            if (unprocessed.Count > 0)
            {
                _logger.LogError(
                    "Failed to write {Count} items after {Max} retries",
                    unprocessed.Values.Sum(v => v.Count),
                    maxRetries);
            }
        }

        _logger.LogInformation("Batch logged {Count} dispatch records", items.Count);
    }

    public async Task UpdateStatusAsync(
        string newsletterType,
        string dispatchTimestamp,
        DispatchStatus status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var updateExpr = "SET #status = :status";
        var exprAttrNames = new Dictionary<string, string>
        {
            { "#status", "Status" }
        };
        var exprAttrValues = new Dictionary<string, AttributeValue>
        {
            { ":status", new AttributeValue { S = status.ToString() } }
        };

        if (!string.IsNullOrEmpty(errorMessage))
        {
            updateExpr += ", ErrorMessage = :error, AttemptCount = AttemptCount + :inc";
            exprAttrValues[":error"] = new AttributeValue { S = errorMessage };
            exprAttrValues[":inc"] = new AttributeValue { N = "1" };
        }

        var request = new UpdateItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "NewsletterType", new AttributeValue { S = newsletterType } },
                { "DispatchTimestamp", new AttributeValue { S = dispatchTimestamp } }
            },
            UpdateExpression = updateExpr,
            ExpressionAttributeNames = exprAttrNames,
            ExpressionAttributeValues = exprAttrValues,
            // Only update if the item exists
            ConditionExpression = "attribute_exists(NewsletterType)"
        };

        try
        {
            await _dynamoDb.UpdateItemAsync(request, cancellationToken);
        }
        catch (ConditionalCheckFailedException)
        {
            _logger.LogWarning(
                "Attempted to update non-existent dispatch log: {NewsletterType}/{DispatchTimestamp}",
                newsletterType,
                dispatchTimestamp);
        }
    }

    public async Task<IReadOnlyList<EmailDispatchLogEntity>> GetDispatchHistoryAsync(
        NewsletterType newsletterType,
        DateTime startTime,
        DateTime endTime,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var request = new QueryRequest
        {
            TableName = TableName,
            KeyConditionExpression = "NewsletterType = :pk AND DispatchTimestamp BETWEEN :start AND :end",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = newsletterType.ToString() } },
                { ":start", new AttributeValue { S = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") } },
                { ":end", new AttributeValue { S = endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + "~" } } // ~ is after Z in ASCII
            },
            Limit = limit,
            ScanIndexForward = false // Newest first
        };

        var response = await _dynamoDb.QueryAsync(request, cancellationToken);
        
        return response.Items
            .Select(AttributesToEntity)
            .ToList();
    }

    public async Task<EmailDispatchLogEntity?> GetDispatchAsync(
        string newsletterType,
        string dispatchTimestamp,
        CancellationToken cancellationToken = default)
    {
        var request = new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "NewsletterType", new AttributeValue { S = newsletterType } },
                { "DispatchTimestamp", new AttributeValue { S = dispatchTimestamp } }
            }
        };

        var response = await _dynamoDb.GetItemAsync(request, cancellationToken);
        
        if (response.Item == null || response.Item.Count == 0)
            return null;

        return AttributesToEntity(response.Item);
    }

    private static Dictionary<string, AttributeValue> EntityToAttributes(EmailDispatchLogEntity entity)
    {
        var attributes = new Dictionary<string, AttributeValue>
        {
            { "NewsletterType", new AttributeValue { S = entity.NewsletterType } },
            { "DispatchTimestamp", new AttributeValue { S = entity.DispatchTimestamp } },
            { "DispatchId", new AttributeValue { S = entity.DispatchId } },
            { "UserId", new AttributeValue { S = entity.UserId } },
            { "Email", new AttributeValue { S = entity.Email } },
            { "Status", new AttributeValue { S = entity.Status } },
            { "AttemptCount", new AttributeValue { N = entity.AttemptCount.ToString() } },
            { "ExpirationTime", new AttributeValue { N = entity.ExpirationTime.ToString() } },
            { "CreatedAt", new AttributeValue { S = entity.CreatedAt } }
        };

        if (!string.IsNullOrEmpty(entity.ErrorMessage))
            attributes["ErrorMessage"] = new AttributeValue { S = entity.ErrorMessage };

        if (!string.IsNullOrEmpty(entity.CorrelationId))
            attributes["CorrelationId"] = new AttributeValue { S = entity.CorrelationId };

        return attributes;
    }

    private static EmailDispatchLogEntity AttributesToEntity(Dictionary<string, AttributeValue> attributes)
    {
        return new EmailDispatchLogEntity
        {
            NewsletterType = attributes["NewsletterType"].S,
            DispatchTimestamp = attributes["DispatchTimestamp"].S,
            DispatchId = attributes.TryGetValue("DispatchId", out var dispatchId) ? dispatchId.S : string.Empty,
            UserId = attributes.TryGetValue("UserId", out var userId) ? userId.S : string.Empty,
            Email = attributes.TryGetValue("Email", out var email) ? email.S : string.Empty,
            Status = attributes.TryGetValue("Status", out var status) ? status.S : DispatchStatus.Pending.ToString(),
            AttemptCount = attributes.TryGetValue("AttemptCount", out var count) ? int.Parse(count.N) : 1,
            ErrorMessage = attributes.TryGetValue("ErrorMessage", out var error) ? error.S : null,
            CorrelationId = attributes.TryGetValue("CorrelationId", out var corrId) ? corrId.S : null,
            ExpirationTime = attributes.TryGetValue("ExpirationTime", out var exp) ? long.Parse(exp.N) : 0,
            CreatedAt = attributes.TryGetValue("CreatedAt", out var created) ? created.S : string.Empty
        };
    }
}


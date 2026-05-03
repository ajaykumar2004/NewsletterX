#!/bin/bash
# =============================================================================
# LocalStack Initialization Script
# =============================================================================
# This script runs automatically when LocalStack starts (via ready.d hook).
# It creates all AWS resources needed for NewsletterX:
# - SNS Topic: newsletter-dispatch
# - SQS Queues: email-send-queue, audit-log-queue (with DLQs)
# - SNS → SQS Subscriptions (fan-out pattern)
# - DynamoDB Table: EmailDispatchLog
#
# ARCHITECTURAL DECISION: Infrastructure as Code (IaC) in a script
# ─────────────────────────────────────────────────────────────────
# For local dev, a shell script is simpler than Terraform/CDK.
# In production, you'd use Terraform, CloudFormation, or CDK.
# The resource names and configurations here mirror what you'd deploy to AWS.
#
# TRANSFERABLE PATTERN: This script structure works for any LocalStack setup.
# Just modify the resource definitions for your services.
# =============================================================================

set -e  # Exit on any error
echo "========================================"
echo "Initializing LocalStack resources..."
echo "========================================"

# LocalStack endpoint
ENDPOINT="http://localhost:4566"
REGION="us-east-1"

# Common AWS CLI options for LocalStack
AWS_OPTS="--endpoint-url=$ENDPOINT --region=$REGION"

# =============================================================================
# 1. Create Dead Letter Queues (DLQs)
# =============================================================================
# WHY DLQs?
# - Messages that fail processing N times move to DLQ instead of being lost
# - Enables debugging: inspect failed messages, understand failure patterns
# - Prevents poison messages from blocking the main queue
#
# PRODUCTION GOTCHA: Set up CloudWatch alarms on DLQ message count!
# A growing DLQ means something is broken.
# =============================================================================

echo "Creating Dead Letter Queues..."

# DLQ for email-send-queue
aws $AWS_OPTS sqs create-queue \
    --queue-name email-send-dlq \
    --attributes '{
        "MessageRetentionPeriod": "1209600"
    }'
# MessageRetentionPeriod: 14 days (max) — gives you time to investigate

# DLQ for audit-log-queue
aws $AWS_OPTS sqs create-queue \
    --queue-name audit-log-dlq \
    --attributes '{
        "MessageRetentionPeriod": "1209600"
    }'

echo "✓ Dead Letter Queues created"

# Get DLQ ARNs for redrive policy
EMAIL_DLQ_ARN=$(aws $AWS_OPTS sqs get-queue-attributes \
    --queue-url "$ENDPOINT/000000000000/email-send-dlq" \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' \
    --output text)

AUDIT_DLQ_ARN=$(aws $AWS_OPTS sqs get-queue-attributes \
    --queue-url "$ENDPOINT/000000000000/audit-log-dlq" \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' \
    --output text)

# =============================================================================
# 2. Create Main SQS Queues
# =============================================================================
# WHY TWO QUEUES (email-send + audit-log)?
# - Separation of concerns: email sending vs audit logging
# - Independent scaling: email queue may need more consumers
# - Independent failure handling: audit failures shouldn't block emails
# - Different retention/visibility requirements
#
# KEY ATTRIBUTES:
# - VisibilityTimeout: 30s — message invisible while processing
#   Set to ~6x your expected processing time
# - ReceiveMessageWaitTimeSeconds: 20s — long polling (reduces API calls)
# - RedrivePolicy: After 3 failures, move to DLQ
# =============================================================================

echo "Creating main SQS queues..."

# Email send queue - consumed by EmailSenderWorker
aws $AWS_OPTS sqs create-queue \
    --queue-name email-send-queue \
    --attributes '{
        "VisibilityTimeout": "30",
        "ReceiveMessageWaitTimeSeconds": "20",
        "MessageRetentionPeriod": "345600",
        "RedrivePolicy": "{\"deadLetterTargetArn\":\"'"$EMAIL_DLQ_ARN"'\",\"maxReceiveCount\":\"3\"}"
    }'
# MessageRetentionPeriod: 4 days — enough time to recover from outages
# maxReceiveCount: 3 — after 3 failed processing attempts, move to DLQ

# Audit log queue - consumed by AuditLogWorker
aws $AWS_OPTS sqs create-queue \
    --queue-name audit-log-queue \
    --attributes '{
        "VisibilityTimeout": "30",
        "ReceiveMessageWaitTimeSeconds": "20",
        "MessageRetentionPeriod": "345600",
        "RedrivePolicy": "{\"deadLetterTargetArn\":\"'"$AUDIT_DLQ_ARN"'\",\"maxReceiveCount\":\"3\"}"
    }'

echo "✓ Main SQS queues created"

# Get queue URLs and ARNs
EMAIL_QUEUE_URL="$ENDPOINT/000000000000/email-send-queue"
AUDIT_QUEUE_URL="$ENDPOINT/000000000000/audit-log-queue"

EMAIL_QUEUE_ARN=$(aws $AWS_OPTS sqs get-queue-attributes \
    --queue-url "$EMAIL_QUEUE_URL" \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' \
    --output text)

AUDIT_QUEUE_ARN=$(aws $AWS_OPTS sqs get-queue-attributes \
    --queue-url "$AUDIT_QUEUE_URL" \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' \
    --output text)

# =============================================================================
# 3. Create SNS Topic
# =============================================================================
# WHY SNS + SQS (Fan-Out Pattern)?
# - One publish to SNS → automatically delivered to ALL subscribed queues
# - Decoupling: Publisher doesn't know/care about consumers
# - Easy to add consumers: just add another SQS subscription
# - Example: Add analytics queue later without changing publish code
#
# ALTERNATIVE: Direct SQS publish
# - Simpler if you have exactly one consumer
# - No fan-out capability
# - Publisher must know queue URL
#
# WHEN TO USE WHICH:
# - 1 consumer, simple flow → Direct SQS
# - Multiple consumers, event-driven → SNS → SQS fan-out
# =============================================================================

echo "Creating SNS topic..."

aws $AWS_OPTS sns create-topic \
    --name newsletter-dispatch

SNS_TOPIC_ARN="arn:aws:sns:$REGION:000000000000:newsletter-dispatch"

echo "✓ SNS topic created: $SNS_TOPIC_ARN"

# =============================================================================
# 4. Subscribe SQS Queues to SNS Topic
# =============================================================================
# This creates the fan-out: one SNS publish → both queues receive the message
#
# IMPORTANT: SQS queues need a policy allowing SNS to send messages!
# In LocalStack this is automatic, but in real AWS you must set it explicitly.
#
# PRODUCTION GOTCHA:
# - Use FilterPolicy to route specific message types to specific queues
# - Example: {"eventType": ["email"]} vs {"eventType": ["audit"]}
# - We're not using filters here — both queues get all messages
# =============================================================================

echo "Subscribing SQS queues to SNS topic..."

# Allow SNS to send to SQS (required in real AWS, LocalStack is permissive)
aws $AWS_OPTS sqs set-queue-attributes \
    --queue-url "$EMAIL_QUEUE_URL" \
    --attributes '{
        "Policy": "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"sqs:SendMessage\",\"Resource\":\"'"$EMAIL_QUEUE_ARN"'\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"'"$SNS_TOPIC_ARN"'\"}}}]}"
    }'

aws $AWS_OPTS sqs set-queue-attributes \
    --queue-url "$AUDIT_QUEUE_URL" \
    --attributes '{
        "Policy": "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"sqs:SendMessage\",\"Resource\":\"'"$AUDIT_QUEUE_ARN"'\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"'"$SNS_TOPIC_ARN"'\"}}}]}"
    }'

# Subscribe email-send-queue to SNS topic
aws $AWS_OPTS sns subscribe \
    --topic-arn "$SNS_TOPIC_ARN" \
    --protocol sqs \
    --notification-endpoint "$EMAIL_QUEUE_ARN" \
    --attributes '{"RawMessageDelivery": "true"}'
# RawMessageDelivery: true — SQS receives the raw message, not wrapped in SNS envelope
# This simplifies message parsing in consumers

# Subscribe audit-log-queue to SNS topic
aws $AWS_OPTS sns subscribe \
    --topic-arn "$SNS_TOPIC_ARN" \
    --protocol sqs \
    --notification-endpoint "$AUDIT_QUEUE_ARN" \
    --attributes '{"RawMessageDelivery": "true"}'

echo "✓ SQS queues subscribed to SNS topic"

# =============================================================================
# 5. Create DynamoDB Table
# =============================================================================
# TABLE: EmailDispatchLog
# PURPOSE: Append-only audit log of all email dispatch attempts
#
# KEY DESIGN:
# - PK (Partition Key): NewsletterType (e.g., "weekly", "breaking-news")
#   → Groups all dispatches for a newsletter together
#   → Enables query: "Show all dispatches for weekly newsletter"
#
# - SK (Sort Key): DispatchTimestamp#DispatchId
#   → Sorted by time within partition
#   → Enables query: "Show dispatches in last 7 days" (BETWEEN condition)
#   → DispatchId suffix ensures uniqueness (same timestamp possible)
#
# WHY DYNAMODB FOR AUDIT LOGS?
# - Append-only writes are fast (no read-modify-write)
# - High write throughput with on-demand capacity
# - Built-in TTL for automatic data expiry (cost savings)
# - No complex queries needed — just time-range lookups
#
# WHY NOT POSTGRESQL?
# - Audit logs can grow huge, affecting query performance
# - TTL is manual (cron job to delete old records)
# - PostgreSQL is better for complex joins, which we don't need here
#
# PRODUCTION GOTCHA: Choose billing mode carefully!
# - PAY_PER_REQUEST: Good for unpredictable traffic, no capacity planning
# - PROVISIONED: Cheaper for steady traffic, but requires capacity planning
# =============================================================================

echo "Creating DynamoDB table..."

aws $AWS_OPTS dynamodb create-table \
    --table-name EmailDispatchLog \
    --attribute-definitions \
        AttributeName=NewsletterType,AttributeType=S \
        AttributeName=DispatchTimestamp,AttributeType=S \
    --key-schema \
        AttributeName=NewsletterType,KeyType=HASH \
        AttributeName=DispatchTimestamp,KeyType=RANGE \
    --billing-mode PAY_PER_REQUEST \
    --tags Key=Environment,Value=local Key=Service,Value=newsletterx

# Enable TTL on the 'ExpirationTime' attribute
# Records will be automatically deleted after this timestamp
# TTL deletion is eventually consistent — may take up to 48 hours after expiry
aws $AWS_OPTS dynamodb update-time-to-live \
    --table-name EmailDispatchLog \
    --time-to-live-specification Enabled=true,AttributeName=ExpirationTime

echo "✓ DynamoDB table created with TTL enabled"

# =============================================================================
# 6. Verification
# =============================================================================

echo ""
echo "========================================"
echo "LocalStack initialization complete!"
echo "========================================"
echo ""
echo "Resources created:"
echo "  SNS Topic:        $SNS_TOPIC_ARN"
echo "  Email Queue:      $EMAIL_QUEUE_URL"
echo "  Audit Queue:      $AUDIT_QUEUE_URL"
echo "  Email DLQ:        $ENDPOINT/000000000000/email-send-dlq"
echo "  Audit DLQ:        $ENDPOINT/000000000000/audit-log-dlq"
echo "  DynamoDB Table:   EmailDispatchLog"
echo ""
echo "Fan-out topology:"
echo "  NewsletterDispatch (SNS)"
echo "       ├── email-send-queue (SQS) → EmailSenderWorker"
echo "       └── audit-log-queue (SQS)  → AuditLogWorker → DynamoDB"
echo ""
echo "Verify with:"
echo "  aws --endpoint-url=$ENDPOINT sns list-topics"
echo "  aws --endpoint-url=$ENDPOINT sqs list-queues"
echo "  aws --endpoint-url=$ENDPOINT dynamodb list-tables"
echo "========================================"


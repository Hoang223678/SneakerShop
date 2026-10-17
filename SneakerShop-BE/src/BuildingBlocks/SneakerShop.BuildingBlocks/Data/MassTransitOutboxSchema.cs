namespace SneakerShop.BuildingBlocks.Data;

public static class MassTransitOutboxSchema
{
    public const string CreateMissingTables = """
        IF OBJECT_ID(N'[dbo].[InboxState]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[InboxState] (
                [Id] bigint IDENTITY(1,1) NOT NULL,
                [MessageId] uniqueidentifier NOT NULL,
                [ConsumerId] uniqueidentifier NOT NULL,
                [LockId] uniqueidentifier NOT NULL,
                [RowVersion] rowversion NULL,
                [Received] datetime2 NOT NULL,
                [ReceiveCount] int NOT NULL,
                [ExpirationTime] datetime2 NULL,
                [Consumed] datetime2 NULL,
                [Delivered] datetime2 NULL,
                [LastSequenceNumber] bigint NULL,
                CONSTRAINT [PK_InboxState] PRIMARY KEY ([Id]),
                CONSTRAINT [AK_InboxState_MessageId_ConsumerId] UNIQUE ([MessageId], [ConsumerId])
            );
        END;

        IF OBJECT_ID(N'[dbo].[OutboxState]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[OutboxState] (
                [OutboxId] uniqueidentifier NOT NULL,
                [LockId] uniqueidentifier NOT NULL,
                [RowVersion] rowversion NULL,
                [Created] datetime2 NOT NULL,
                [Delivered] datetime2 NULL,
                [LastSequenceNumber] bigint NULL,
                CONSTRAINT [PK_OutboxState] PRIMARY KEY ([OutboxId])
            );
        END;

        IF OBJECT_ID(N'[dbo].[OutboxMessage]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[OutboxMessage] (
                [SequenceNumber] bigint IDENTITY(1,1) NOT NULL,
                [EnqueueTime] datetime2 NULL,
                [SentTime] datetime2 NOT NULL,
                [Headers] nvarchar(max) NULL,
                [Properties] nvarchar(max) NULL,
                [InboxMessageId] uniqueidentifier NULL,
                [InboxConsumerId] uniqueidentifier NULL,
                [OutboxId] uniqueidentifier NULL,
                [MessageId] uniqueidentifier NOT NULL,
                [ContentType] nvarchar(256) NOT NULL,
                [MessageType] nvarchar(max) NOT NULL,
                [Body] nvarchar(max) NOT NULL,
                [ConversationId] uniqueidentifier NULL,
                [CorrelationId] uniqueidentifier NULL,
                [InitiatorId] uniqueidentifier NULL,
                [RequestId] uniqueidentifier NULL,
                [SourceAddress] nvarchar(256) NULL,
                [DestinationAddress] nvarchar(256) NULL,
                [ResponseAddress] nvarchar(256) NULL,
                [FaultAddress] nvarchar(256) NULL,
                [ExpirationTime] datetime2 NULL,
                CONSTRAINT [PK_OutboxMessage] PRIMARY KEY ([SequenceNumber]),
                CONSTRAINT [FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId]
                    FOREIGN KEY ([InboxMessageId], [InboxConsumerId])
                    REFERENCES [dbo].[InboxState] ([MessageId], [ConsumerId]),
                CONSTRAINT [FK_OutboxMessage_OutboxState_OutboxId]
                    FOREIGN KEY ([OutboxId]) REFERENCES [dbo].[OutboxState] ([OutboxId])
            );
        END;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_InboxState_Delivered' AND [object_id] = OBJECT_ID(N'[dbo].[InboxState]'))
            CREATE INDEX [IX_InboxState_Delivered] ON [dbo].[InboxState] ([Delivered]);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OutboxMessage_EnqueueTime' AND [object_id] = OBJECT_ID(N'[dbo].[OutboxMessage]'))
            CREATE INDEX [IX_OutboxMessage_EnqueueTime] ON [dbo].[OutboxMessage] ([EnqueueTime]);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OutboxMessage_ExpirationTime' AND [object_id] = OBJECT_ID(N'[dbo].[OutboxMessage]'))
            CREATE INDEX [IX_OutboxMessage_ExpirationTime] ON [dbo].[OutboxMessage] ([ExpirationTime]);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber' AND [object_id] = OBJECT_ID(N'[dbo].[OutboxMessage]'))
            CREATE UNIQUE INDEX [IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber]
                ON [dbo].[OutboxMessage] ([InboxMessageId], [InboxConsumerId], [SequenceNumber])
                WHERE [InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OutboxMessage_OutboxId_SequenceNumber' AND [object_id] = OBJECT_ID(N'[dbo].[OutboxMessage]'))
            CREATE UNIQUE INDEX [IX_OutboxMessage_OutboxId_SequenceNumber]
                ON [dbo].[OutboxMessage] ([OutboxId], [SequenceNumber])
                WHERE [OutboxId] IS NOT NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_OutboxState_Created' AND [object_id] = OBJECT_ID(N'[dbo].[OutboxState]'))
            CREATE INDEX [IX_OutboxState_Created] ON [dbo].[OutboxState] ([Created]);
        """;
}

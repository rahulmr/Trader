﻿CREATE TABLE [dbo].[Order]
(
    [SymbolId] INT NOT NULL,
    [OrderId] BIGINT NOT NULL,
    [OrderListId] BIGINT NOT NULL,
    [ClientOrderId] NVARCHAR(100) NOT NULL,
    [Price] DECIMAL (18,8) NOT NULL,
    [OriginalQuantity] DECIMAL (18,8) NOT NULL,
    [ExecutedQuantity] DECIMAL (18,8) NOT NULL,
    [CummulativeQuoteQuantity] DECIMAL (18,8) NOT NULL,
    [OriginalQuoteOrderQuantity] DECIMAL (18,8) NOT NULL,
    [Status] INT NOT NULL,
    [TimeInForce] INT NOT NULL,
    [Type] INT NOT NULL,
    [Side] INT NOT NULL,
    [StopPrice] DECIMAL (18,8) NOT NULL,
    [IcebergQuantity] DECIMAL (18,8) NOT NULL,
    [Time] DATETIME2(7) NOT NULL,
    [UpdateTime] DATETIME2(7) NOT NULL,
    [IsWorking] BIT NOT NULL,

    /* helpers */
    [IsTransient] AS (CONVERT (BIT, CASE WHEN [Status] = 1 OR [Status] = 2 OR [Status] = 5 THEN 1 ELSE 0 END)) PERSISTED NOT NULL,

    CONSTRAINT [PK_Order] PRIMARY KEY CLUSTERED
    (
        [SymbolId],
        [OrderId]
    )
)
GO

/* this index helps to quickly identify all transient orders for a symbol */
CREATE NONCLUSTERED INDEX [NCI_Order_SymbolId_IsTransient]
ON [dbo].[Order]
(
    [SymbolId],
    [IsTransient]
)
GO
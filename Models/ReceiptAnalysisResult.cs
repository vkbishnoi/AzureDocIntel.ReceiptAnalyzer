namespace AzureDocIntel.ReceiptAnalyzer.Models;

public sealed class ReceiptAnalysisResult
{
    public ReceiptSummary Summary { get; init; } = new();

    public ReceiptItemCollection Items { get; init; } = new();

    public ReceiptOcrData Ocr { get; init; } = new();

    public ReceiptModelInfo Model { get; init; } = new();
}

public sealed class ReceiptSummary
{
    public ReceiptClassification Classification { get; init; }

    public string? MerchantName { get; init; }
    public string? MerchantAddress { get; init; }
    public string? MerchantPhoneNumber { get; init; }

    public DateTime? TransactionDate { get; init; }
    public TimeSpan? TransactionTime { get; init; }

    public ReceiptAmount? Total { get; init; }
    public ReceiptAmount? Subtotal { get; init; }
    public ReceiptAmount? Tax { get; init; }
}

public sealed class ReceiptItemCollection
{
    public IReadOnlyList<ReceiptLineItem> Lines { get; init; } = [];
}

public sealed class ReceiptLineItem
{
    public string? Description { get; init; }

    public decimal? Quantity { get; init; }

    public ReceiptAmount? UnitPrice { get; init; }

    public ReceiptAmount? TotalPrice { get; init; }

    public float? Confidence { get; init; }
}

public sealed class ReceiptOcrData
{
    public string RawText { get; init; } = string.Empty;

    public float? DocumentConfidence { get; init; }
}

public sealed class ReceiptModelInfo
{
    public string Provider { get; init; } = "Azure";

    public string ModelName { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;
}

public sealed class ReceiptAmount
{
    public decimal? Value { get; init; }

    public string? Currency { get; init; }
}

public enum ReceiptClassification : byte
{
    LooksLikeReceipt = 0,
    Uncertain = 1,
    NotAReceipt = 2,
    ProbablyReceipt = 3
}
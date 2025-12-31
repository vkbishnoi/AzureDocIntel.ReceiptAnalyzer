using Azure;
using Azure.AI.DocumentIntelligence;
using AzureDocIntel.ReceiptAnalyzer.Models;

namespace AzureDocIntel.ReceiptAnalyzer;

public sealed class ReceiptImageAnalyzer
{
    private readonly DocumentIntelligenceClient client;

    private const string ReceiptModelId = "prebuilt-receipt";

    private const float HighConfidenceThreshold = 0.85f;
    private const float MediumConfidenceThreshold = 0.70f;
    private const float MinimumFieldConfidence = 0.60f;

    public ReceiptImageAnalyzer(string endpoint, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException(nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException(nameof(apiKey));
        }

        client = new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public Task<ReceiptAnalysisResult> AnalyzeAsync(byte[] receiptImage, CancellationToken cancellationToken = default)
    {
        return AnalyzeAsync(receiptImage, includeOcrContent: false, cancellationToken);
    }

    public async Task<ReceiptAnalysisResult> AnalyzeAsync(byte[] receiptImage, bool includeOcrContent,
                                                            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receiptImage);

        if (receiptImage.Length == 0)
        {
            throw new ArgumentException("Receipt image cannot be empty", nameof(receiptImage));
        }

        try
        {
            var binaryData = BinaryData.FromBytes(receiptImage);

            var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, ReceiptModelId, binaryData,
                                                                cancellationToken: cancellationToken);

            return MapToResult(operation.Value, includeOcrContent);
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to analyze receipt image.", ex);
        }
    }

    // -------------------------------------------------
    // Mapping (based directly on your working code)
    // -------------------------------------------------

    private static ReceiptAnalysisResult MapToResult(AnalyzeResult result, bool includeOcrContent)
    {
        var doc = result.Documents is { Count: > 0 } ? result.Documents[0] : null;

        return new ReceiptAnalysisResult
        {
            Summary = new ReceiptSummary
            {
                Classification = Classify(doc),

                MerchantName = GetStringField(doc, "MerchantName"),
                MerchantAddress = GetStringField(doc, "MerchantAddress"),
                MerchantPhoneNumber = GetStringField(doc, "MerchantPhoneNumber"),

                // Transaction details
                TransactionDate = GetDateField(doc, "TransactionDate"),
                TransactionTime = GetTimeField(doc, "TransactionTime"),

                Subtotal = GetCurrencyAmount(doc, "Subtotal"),
                Tax = GetCurrencyAmount(doc, "TotalTax"),
                Total = GetCurrencyAmount(doc, "Total")
            },

            Items = new ReceiptItemCollection
            {
                Lines = GetLineItems(doc)
            },

            Ocr = includeOcrContent ? new ReceiptOcrData
            {
                RawText = result.Content ?? string.Empty,
                DocumentConfidence = doc?.Confidence
            }
                : new ReceiptOcrData(),

            Model = new ReceiptModelInfo
            {
                Provider = "AzureAI",
                ModelId = result.ModelId,
                ModelName = ReceiptModelId
            }
        };
    }

    // -------------------------------------------------
    // Classification (unchanged logic)
    // -------------------------------------------------

    private static ReceiptClassification Classify(AnalyzedDocument? doc)
    {
        if (doc == null)
        {
            return ReceiptClassification.NotAReceipt;
        }

        return doc.Confidence switch
        {
            >= HighConfidenceThreshold => ReceiptClassification.LooksLikeReceipt,
            >= MediumConfidenceThreshold => ReceiptClassification.ProbablyReceipt,
            _ => ReceiptClassification.Uncertain
        };
    }

    // -------------------------------------------------
    // Line items (unchanged SDK usage)
    // -------------------------------------------------

    private static IReadOnlyList<ReceiptLineItem> GetLineItems(AnalyzedDocument? doc)
    {
        if (doc?.Fields == null || !doc.Fields.TryGetValue("Items", out var itemsField) ||
            itemsField.FieldType != DocumentFieldType.List || itemsField.ValueList == null)
        {
            return [];
        }

        var items = new List<ReceiptLineItem>();

        foreach (var item in itemsField.ValueList)
        {
            if (item.FieldType != DocumentFieldType.Dictionary ||
                item.ValueDictionary == null)
            {
                continue;
            }

            var lineItem = new ReceiptLineItem
            {
                Description = GetStringFromDict(item, "Description"),
                Quantity = GetDoubleFromDict(item, "Quantity"),
                UnitPrice = GetCurrencyAmountFromDict(item, "Price"),
                TotalPrice = GetCurrencyAmountFromDict(item, "TotalPrice"),
                Confidence = item.Confidence
            };

            if (!string.IsNullOrWhiteSpace(lineItem.Description) || lineItem.TotalPrice?.Value != null)
            {
                items.Add(lineItem);
            }
        }

        return items;
    }

    // -------------------------------------------------
    // Document-level extractors (unchanged patterns)
    // -------------------------------------------------

    private static string? GetStringField(AnalyzedDocument? doc, string fieldName)
    {
        if (doc?.Fields == null || !doc.Fields.TryGetValue(fieldName, out var field) ||
            field.Confidence < MinimumFieldConfidence)
        {
            return null;
        }

        return field.FieldType == DocumentFieldType.String ? field.ValueString : null;
    }

    private static DateTime? GetDateField(AnalyzedDocument? doc, string fieldName)
    {
        if (doc?.Fields == null || !doc.Fields.TryGetValue(fieldName, out var field) ||
            field.Confidence < MinimumFieldConfidence)
        {
            return null;
        }

        return field.FieldType == DocumentFieldType.Date && field.ValueDate != null
            ? field.ValueDate.Value.DateTime
            : null;
    }

    private static TimeSpan? GetTimeField(AnalyzedDocument? doc, string fieldName)
    {
        if (doc?.Fields == null || !doc.Fields.TryGetValue(fieldName, out var field) ||
            field.Confidence < MinimumFieldConfidence)
        {
            return null;
        }

        return field.FieldType == DocumentFieldType.Time
            ? field.ValueTime
            : null;
    }

    private static ReceiptAmount? GetCurrencyAmount(AnalyzedDocument? doc, string fieldName)
    {
        if (doc?.Fields == null || !doc.Fields.TryGetValue(fieldName, out var field) ||
            field.Confidence < MinimumFieldConfidence)
        {
            return null;
        }

        if (field.FieldType == DocumentFieldType.Currency && field.ValueCurrency != null)
        {
            return new ReceiptAmount
            {
                Value = decimal.Round((decimal)field.ValueCurrency.Amount, 2),
                Currency = field.ValueCurrency.CurrencyCode
            };
        }

        if (field.FieldType == DocumentFieldType.Double && field.ValueDouble.HasValue)
        {
            return new ReceiptAmount
            {
                Value = decimal.Round((decimal)field.ValueDouble.Value, 2)
            };
        }

        if (field.FieldType == DocumentFieldType.Int64 && field.ValueInt64.HasValue)
        {
            return new ReceiptAmount
            {
                Value = field.ValueInt64.Value
            };
        }

        return null;
    }

    // -------------------------------------------------
    // Dictionary helpers (unchanged patterns)
    // -------------------------------------------------

    private static string? GetStringFromDict(DocumentField dictField, string key)
    {
        if (dictField.ValueDictionary == null || !dictField.ValueDictionary.TryGetValue(key, out var field) ||
            field.Confidence < MinimumFieldConfidence)
        {
            return null;
        }

        return field.FieldType == DocumentFieldType.String ? field.ValueString : null;
    }

    private static decimal? GetDoubleFromDict(DocumentField dictField, string key)
    {
        if (dictField.ValueDictionary == null || !dictField.ValueDictionary.TryGetValue(key, out var field) ||
            field.Confidence < MinimumFieldConfidence)
        {
            return null;
        }

        if (field.FieldType == DocumentFieldType.Double && field.ValueDouble.HasValue)
        {
            return (decimal)field.ValueDouble.Value;
        }

        if (field.FieldType == DocumentFieldType.Int64 && field.ValueInt64.HasValue)
        {
            return field.ValueInt64.Value;
        }

        return null;
    }

    private static ReceiptAmount? GetCurrencyAmountFromDict(DocumentField dictField, string key)
    {
        if (dictField.ValueDictionary == null || !dictField.ValueDictionary.TryGetValue(key, out var field) ||
            field.Confidence < MinimumFieldConfidence)
        {
            return null;
        }

        if (field.FieldType == DocumentFieldType.Currency && field.ValueCurrency != null)
        {
            return new ReceiptAmount
            {
                Value = decimal.Round((decimal)field.ValueCurrency.Amount, 2),
                Currency = field.ValueCurrency.CurrencyCode
            };
        }

        if (field.FieldType == DocumentFieldType.Double && field.ValueDouble.HasValue)
        {
            return new ReceiptAmount
            {
                Value = decimal.Round((decimal)field.ValueDouble.Value, 2)
            };
        }

        if (field.FieldType == DocumentFieldType.Int64 && field.ValueInt64.HasValue)
        {
            return new ReceiptAmount
            {
                Value = field.ValueInt64.Value
            };
        }

        return null;
    }
}

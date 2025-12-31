# AzureDocIntel.ReceiptAnalyzer

A lightweight .NET library for analyzing receipt images using **Azure AI Document Intelligence**
and the **prebuilt-receipt** model.

This project provides a simple, dependency-free wrapper over the Azure SDK, exposing
clean, strongly-typed results without requiring dependency injection or configuration files.

---

## Features

- Uses Azure AI Document Intelligence (prebuilt-receipt model)
- No dependency injection
- No appsettings or configuration files
- Explicit constructor-based usage
- Strongly-typed receipt results
- Optional OCR text inclusion
- Preserves Azure confidence values
- Suitable for console apps, background jobs, and backend services

---

## Requirements

- .NET 10
- Azure AI Document Intelligence resource
- Azure.AI.DocumentIntelligence SDK

---

## Installation

Once published to NuGet:

```bash
dotnet add package AzureDocIntel.ReceiptAnalyzer
```

## Usage
```csharp
using AzureDocIntel.ReceiptAnalyzer;

var analyzer = new ReceiptImageAnalyzer(
    endpoint: "https://<your-resource>.cognitiveservices.azure.com/",
    apiKey: "<your-api-key>");

byte[] imageBytes = File.ReadAllBytes("receipt.jpg");

var result = await analyzer.AnalyzeAsync(
    imageBytes,
    includeOcrContent: true);

Console.WriteLine(result.Summary.MerchantName);


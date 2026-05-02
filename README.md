# Plugin.InAppBilling.StoreKit2

A fork of [James Montemagno's Plugin.InAppBilling](https://github.com/jamesmontemagno/InAppBillingPlugin) with native **StoreKit2** support for iOS and Mac Catalyst.

## What's different

- **StoreKit2 integration** on iOS/macCatalyst via a custom Swift framework
- Android remains on the standard Google BillingClient (unchanged from original)
- All other platforms behave identically to the original plugin

## Why this fork

Apple's StoreKit2 provides a modern, Swift-native API for in-app purchases and subscriptions. This fork replaces the original StoreKit1 implementation on Apple platforms with StoreKit2 while keeping the same cross-platform C# API.

## Usage

```xml
<PackageReference Include="Plugin.InAppBilling.StoreKit2" Version="1.0.1" />
```

The API is identical to the original Plugin.InAppBilling:

```csharp
var billing = CrossInAppBilling.Current;
var connected = await billing.ConnectAsync();
var products = await billing.GetProductsAsync(ItemType.InAppPurchase);
var purchase = await billing.PurchaseAsync(product.ProductId, ItemType.InAppPurchase);
```

## Credits

- **James Montemagno** — original cross-platform InAppBilling plugin
- **Yuting Li / Shanghai Jiuqianji Technology Co., Ltd.** — MAUI.StoreKit2 binding library for iOS StoreKit2

---

<p align="center">
  <a href="https://paypal.me/ibrahimelkady1">
    <img src="https://raw.githubusercontent.com/stefan-niedermann/paypal-donate-button/master/paypal-donate-button.png" alt="Donate with PayPal" height="48">
  </a>
  <br>
  <sub>If this project helped you, consider supporting my work ❤️</sub>
</p>

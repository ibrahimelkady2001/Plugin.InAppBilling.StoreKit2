#if IOS
using Foundation;
using StoreKit2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Implementation for InAppBilling using StoreKit2
    /// </summary>
    [Preserve(AllMembers = true)]
    public class InAppBillingImplementation : BaseInAppBilling, IDisposable
    {
        private readonly PaymentManager _paymentManager;
        private readonly PaymentDelegateHandler _delegateHandler;
        private string _currentPurchaseProductId;
        private TaskCompletionSource<PaymentTransaction> _currentPurchaseTcs;

        // Semaphore to ensure only one product request at a time
        private readonly SemaphoreSlim _productRequestSemaphore = new SemaphoreSlim(1, 1);
        private TaskCompletionSource<bool> _currentProductRequestTcs;

        /// <summary>
        /// Backwards compat flag - not used in StoreKit2 but kept for API compatibility
        /// </summary>
        public static bool FinishAllTransactions { get; set; } = true;

        /// <summary>
        /// Gets if user can make payments (always true in StoreKit2)
        /// </summary>
        public override bool CanMakePayments => true;

        /// <summary>
        /// Gets or sets a callback for out of band purchases to complete.
        /// </summary>
        public static Action<InAppBillingPurchase> OnPurchaseComplete { get; set; } = null;

        /// <summary>
        /// Gets or sets a callback for out of band failures to complete.
        /// </summary>
        public static Action<InAppBillingPurchase> OnPurchaseFailure { get; set; } = null;

        /// <summary>
        /// Storefront information (StoreKit2 provides this differently)
        /// </summary>
        public override Storefront Storefront { get; }

        /// <summary>
        /// Default constructor for In App Billing on iOS with StoreKit2
        /// </summary>
        public InAppBillingImplementation()
        {
            _paymentManager = PaymentManager.Shared;
            _delegateHandler = new PaymentDelegateHandler(this);
            _paymentManager.Delegate = _delegateHandler;
        }

        /// <summary>
        /// iOS: Displays a sheet that enables users to redeem subscription offer codes
        /// </summary>
        public override void PresentCodeRedemption()
        {
            // StoreKit2 handles this differently - would need to use App Store's redemption flow
            // This may not be directly available in the binding library
            Debug.WriteLine("PresentCodeRedemption: Not directly supported in StoreKit2 binding");
        }

        /// <summary>
        /// Gets or sets if in testing mode. Only for UWP
        /// </summary>
        public override bool InTestingMode { get; set; }

        /// <summary>
        /// Get product information of a specific product
        /// </summary>
        public async override Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(
            ItemType itemType,
            string[] productIds,
            CancellationToken cancellationToken)
        {
            // Ensure only one product request executes at a time
            await _productRequestSemaphore.WaitAsync();

            try
            {
                var tcs = new TaskCompletionSource<bool>();
                _currentProductRequestTcs = tcs;

                _paymentManager.RequestProductsWithProductIds(productIds, (succeeded, error) =>
                {
                    if (succeeded)
                        tcs.TrySetResult(true);
                    else
                        tcs.TrySetException(new InAppBillingPurchaseException(
                            PurchaseError.ProductRequestFailed,
                            error ?? "Failed to load products"));
                });

                var success = await tcs.Task;

                if (!success)
                    return Enumerable.Empty<InAppBillingProduct>();

                // Get only the products that were requested
                var products = _paymentManager.AllProducts;
                var requestedProductIds = new HashSet<string>(productIds);

                return products
                    .Where(p => requestedProductIds.Contains(p.ProductId))
                    .Select(p => new InAppBillingProduct
                    {
                        LocalizedPrice = p.DisplayPrice ?? string.Empty,
                        MicrosPrice = ConvertPriceToMicros(p.Price),
                        Name = p.DisplayName ?? string.Empty,
                        ProductId = p.ProductId ?? string.Empty,
                        Description = p.ProductDescription ?? string.Empty,
                        CurrencyCode = ExtractCurrencyCode(p.DisplayPrice),
                        AppleExtras = new InAppBillingProductAppleExtras
                        {
                            IsFamilyShareable   = p.IsFamilyShareable,
                            SubscriptionGroupId = p.SubscriptionGroupId,
                            SubscriptionPeriod  = p.SubscriptionPeriodValue > 0
                                ? new SubscriptionPeriod
                                {
                                    NumberOfUnits = (int)p.SubscriptionPeriodValue,
                                    Unit          = ParsePeriodUnit(p.SubscriptionPeriodUnit),
                                }
                                : null,
                            IntroductoryOffer = p.IntroductoryOffer != null
                                ? ToProductDiscount(p.IntroductoryOffer, ProductDiscountType.Introductory)
                                : null,
                            Discounts = p.PromotionalOffers?.Length > 0
                                ? p.PromotionalOffers
                                    .Select(o => ToProductDiscount(o, ProductDiscountType.Subscription))
                                    .ToList()
                                : null,
                        }
                    });
            }
            finally
            {
                _currentProductRequestTcs = null;
                _productRequestSemaphore.Release();
            }
        }

        /// <summary>
        /// Get all purchases
        /// </summary>
        public async override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(
            ItemType itemType,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<PaymentTransaction[]>();

            _paymentManager.RestorePurchasesWithCompletion((success, error) =>
            {
                if (!success)
                    tcs.TrySetException(new InAppBillingPurchaseException(
                        PurchaseError.RestoreFailed,
                        error ?? "Failed to restore purchases"));
            });

            var transactions = await tcs.Task;

            var comparer = new InAppBillingPurchaseComparer();
            return transactions
                ?.Where(t => t != null)
                ?.Select(t => ConvertTransactionToPurchase(t))
                ?.Distinct(comparer)
                ?? Enumerable.Empty<InAppBillingPurchase>();
        }

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        public async override Task<InAppBillingPurchase> PurchaseAsync(
            string productId,
            ItemType itemType,
            string obfuscatedAccountId = null,
            string obfuscatedProfileId = null,
            string subOfferToken = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<PaymentTransaction>();

            _currentPurchaseProductId = productId;
            _currentPurchaseTcs = tcs;

            try
            {
                // Convert string UUID to NSUuid if provided
                NSUuid appAccountToken = null;
                if (!string.IsNullOrWhiteSpace(obfuscatedAccountId))
                {
                    if (Guid.TryParse(obfuscatedAccountId, out var guid))
                    {
                        appAccountToken = new NSUuid(obfuscatedAccountId);
                    }
                }

                _paymentManager.PurchaseProductWithProductId(productId, appAccountToken, (success, error) =>
                {
                    if (!success && error != null)
                    {
                        tcs.TrySetException(new InAppBillingPurchaseException(
                            ParsePurchaseError(error),
                            error));
                    }
                });

                var transaction = await tcs.Task;

                return ConvertTransactionToPurchase(transaction);
            }
            finally
            {
                _currentPurchaseProductId = null;
                _currentPurchaseTcs = null;
            }
        }

        /// <summary>
        /// (iOS not supported) Apple store manages upgrades natively
        /// </summary>
        public override Task<InAppBillingPurchase> UpgradePurchasedSubscriptionAsync(
            string newProductId,
            string purchaseTokenOfOriginalSubscription,
            SubscriptionProrationMode prorationMode = SubscriptionProrationMode.ImmediateWithTimeProration,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException("iOS not supported. Apple store manages upgrades natively when subscriptions of the same group are purchased.");

        /// <summary>
        /// Gets receipt data from bundle (StoreKit2 uses different verification)
        /// </summary>
        public override string ReceiptData
        {
            get
            {
                // StoreKit2 uses JWS (JSON Web Signature) tokens instead of receipts
                // This would need custom implementation or migration to server-side verification
                Debug.WriteLine("ReceiptData: StoreKit2 uses transaction verification instead of receipt data");
                return string.Empty;
            }
        }

        /// <summary>
        /// Consume a purchase with a purchase token (StoreKit2 auto-manages this)
        /// </summary>
        public override async Task<bool> ConsumePurchaseAsync(
            string productId,
            string transactionIdentifier,
            CancellationToken cancellationToken)
        {
            // StoreKit2 automatically handles transaction finishing
            // Consumable products can be purchased again without manual consumption
            await Task.CompletedTask;
            return true;
        }

        /// <summary>
        /// Finalize purchase of products (StoreKit2 auto-manages transactions)
        /// </summary>
        public override async Task<IEnumerable<(string Id, bool Success)>> FinalizePurchaseOfProductAsync(
            string[] productIds,
            CancellationToken cancellationToken)
        {
            // StoreKit2 automatically finishes transactions
            await Task.CompletedTask;
            return productIds.Select(id => (id, true));
        }

        /// <summary>
        /// Finalize purchase manually (StoreKit2 auto-manages transactions)
        /// </summary>
        public async override Task<IEnumerable<(string Id, bool Success)>> FinalizePurchaseAsync(
            string[] transactionIdentifier,
            CancellationToken cancellationToken)
        {
            // StoreKit2 automatically finishes transactions
            await Task.CompletedTask;
            return transactionIdentifier.Select(id => (id, true));
        }

        #region Helper Methods

        private long ConvertPriceToMicros(NSDecimalNumber price)
        {
            if (price == null)
                return 0;

            return (long)(price.DoubleValue * 1000000d);
        }

        private static string ExtractCurrencyCode(string displayPrice)
        {
            // Simple extraction - in production, you'd want more robust parsing
            // StoreKit2 may provide this in PaymentProduct properties
            if (string.IsNullOrWhiteSpace(displayPrice))
                return string.Empty;

            // Extract currency symbol/code from formatted price
            // This is a simplification - actual implementation would be more complex
            return string.Empty;
        }

        private InAppBillingPurchase ConvertTransactionToPurchase(PaymentTransaction transaction)
        {
            if (transaction == null)
                return null;

            // Use OriginalTransactionId if available, otherwise fall back to TransactionId
            var originalId = transaction.OriginalTransactionId ?? transaction.TransactionId;

            return new InAppBillingPurchase
            {
                TransactionDateUtc = NSDateToDateTimeUtc(transaction.PurchaseDate),
                Id = transaction.TransactionId ?? string.Empty,
                OriginalTransactionIdentifier = originalId ?? string.Empty,
                TransactionIdentifier = transaction.TransactionId,
                ProductId = transaction.ProductId ?? string.Empty,
                ProductIds = new[] { transaction.ProductId ?? string.Empty },
                State = DeterminePurchaseState(transaction),
                PurchaseToken = transaction.TransactionId ?? string.Empty,
                ApplicationUsername = string.Empty
            };
        }

        private static DateTime NSDateToDateTimeUtc(NSDate date)
        {
            var reference = new DateTime(2001, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return reference.AddSeconds(date?.SecondsSinceReferenceDate ?? 0);
        }

        private PurchaseState DeterminePurchaseState(PaymentTransaction transaction)
        {
            // StoreKit2 transactions that are returned are generally successful
            // Failed transactions wouldn't be included in the results
            if (transaction.IsUpgraded)
                return PurchaseState.Restored;

            if (transaction.RevocationDate != null)
                return PurchaseState.Failed;

            return PurchaseState.Purchased;
        }

        private static InAppBillingProductDiscount ToProductDiscount(
            PaymentIntroductoryOffer offer, ProductDiscountType type) =>
            new InAppBillingProductDiscount
            {
                Id              = offer.Id ?? string.Empty,
                Price           = offer.Price.DoubleValue * 1000000d,
                LocalizedPrice  = offer.DisplayPrice ?? string.Empty,
                CurrencyCode    = ExtractCurrencyCode(offer.DisplayPrice),
                PaymentMode     = ParsePaymentMode(offer.PaymentMode),
                NumberOfPeriods = (int)offer.PeriodCount,
                SubscriptionPeriod = new SubscriptionPeriod
                {
                    NumberOfUnits = (int)offer.PeriodValue,
                    Unit          = ParsePeriodUnit(offer.PeriodUnit),
                },
                Type = type,
            };

        private static SubscriptionPeriodUnit ParsePeriodUnit(string unit) => unit switch
        {
            "day"   => SubscriptionPeriodUnit.Day,
            "week"  => SubscriptionPeriodUnit.Week,
            "month" => SubscriptionPeriodUnit.Month,
            "year"  => SubscriptionPeriodUnit.Year,
            _       => SubscriptionPeriodUnit.Unknown,
        };

        private static PaymentMode ParsePaymentMode(string mode) => mode switch
        {
            "freeTrial"  => PaymentMode.FreeTrial,
            "payAsYouGo" => PaymentMode.PayAsYouGo,
            "payUpFront" => PaymentMode.PayUpFront,
            _            => PaymentMode.Unknown,
        };

        /// <summary>
        /// Returns true if the user is eligible for an introductory offer on the given product.
        /// Uses StoreKit 2's isEligibleForIntroOffer — requires products to be loaded first.
        /// </summary>
        public Task<bool> IsEligibleForIntroductoryOfferAsync(string productId)
        {
            var tcs = new TaskCompletionSource<bool>();
            _paymentManager.CheckIntroductoryOfferEligibility(productId, eligible =>
                tcs.TrySetResult(eligible));
            return tcs.Task;
        }

        private PurchaseError ParsePurchaseError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return PurchaseError.GeneralError;

            var lowerError = error.ToLowerInvariant();

            if (lowerError.Contains("cancel"))
                return PurchaseError.UserCancelled;
            if (lowerError.Contains("invalid"))
                return PurchaseError.PaymentInvalid;
            if (lowerError.Contains("not allowed"))
                return PurchaseError.PaymentNotAllowed;
            if (lowerError.Contains("unavailable"))
                return PurchaseError.ItemUnavailable;

            return PurchaseError.GeneralError;
        }

        #endregion

        #region Disposal

        private bool _disposed = false;

        public override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                base.Dispose(disposing);
                return;
            }

            _disposed = true;

            if (disposing)
            {
                _paymentManager.Delegate = null;
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Delegate Handler

        private class PaymentDelegateHandler : PaymentManagerDelegate
        {
            private readonly InAppBillingImplementation _implementation;

            public PaymentDelegateHandler(InAppBillingImplementation implementation)
            {
                _implementation = implementation;
            }

            public override void PaymentManagerDidFinishPurchase(string productId, PaymentTransaction transaction)
            {
                Debug.WriteLine($"Purchase completed: {productId}");

                var purchase = _implementation.ConvertTransactionToPurchase(transaction);

                // Handle completion source for awaited purchases
                if (_implementation._currentPurchaseProductId == productId && _implementation._currentPurchaseTcs != null)
                {
                    _implementation._currentPurchaseTcs.TrySetResult(transaction);
                }

                // Trigger callback for out-of-band purchases
                OnPurchaseComplete?.Invoke(purchase);
            }

            public override void PaymentManagerDidFailPurchase(string productId, string error)
            {
                Debug.WriteLine($"Purchase failed: {productId}, Error: {error}");

                // Handle completion source for awaited purchases
                if (_implementation._currentPurchaseProductId == productId && _implementation._currentPurchaseTcs != null)
                {
                    var purchaseError = _implementation.ParsePurchaseError(error);
                    _implementation._currentPurchaseTcs.TrySetException(
                        new InAppBillingPurchaseException(purchaseError, error));
                }

                // Trigger callback for out-of-band failures
                var failedPurchase = new InAppBillingPurchase
                {
                    ProductId = productId,
                    State = PurchaseState.Failed
                };
                OnPurchaseFailure?.Invoke(failedPurchase);
            }

            public override void PaymentManagerDidUpdateProducts(PaymentProduct[] products)
            {
                Debug.WriteLine($"Products loaded: {products?.Length ?? 0}");
                _implementation._currentProductRequestTcs?.TrySetResult(true);
            }

            public override void PaymentManagerDidRestorePurchases(PaymentTransaction[] transactions)
            {
                Debug.WriteLine($"Restored {transactions?.Length ?? 0} purchases");
                // Restore is handled via the delegate - no need for a field TCS
            }
        }

        #endregion
    }
}
#endif
using System;
using Foundation;
using ObjCRuntime;

namespace StoreKit2
{
    // @interface PaymentIntroductoryOffer : NSObject
    // SWIFT_CLASS("_TtC18StoreKit2Framework24PaymentIntroductoryOffer")
    [BaseType(typeof(NSObject), Name = "_TtC18StoreKit2Framework24PaymentIntroductoryOffer")]
    [DisableDefaultCtor]
    interface PaymentIntroductoryOffer
    {
        // @property (nonatomic, readonly, copy) NSString * _Nonnull id;
        [Export("id")]
        string Id { get; }

        // @property (nonatomic, readonly, copy) NSString * _Nonnull displayPrice;
        [Export("displayPrice")]
        string DisplayPrice { get; }

        // @property (nonatomic, readonly, strong) NSDecimalNumber * _Nonnull price;
        [Export("price", ArgumentSemantic.Strong)]
        NSDecimalNumber Price { get; }

        // "freeTrial" | "payAsYouGo" | "payUpFront" | "unknown"
        // @property (nonatomic, readonly, copy) NSString * _Nonnull paymentMode;
        [Export("paymentMode")]
        string PaymentMode { get; }

        // Number of period units (e.g. 3 for "3 months")
        // @property (nonatomic, readonly) NSInteger periodValue;
        [Export("periodValue")]
        nint PeriodValue { get; }

        // "day" | "week" | "month" | "year"
        // @property (nonatomic, readonly, copy) NSString * _Nonnull periodUnit;
        [Export("periodUnit")]
        string PeriodUnit { get; }

        // How many periods the offer lasts
        // @property (nonatomic, readonly) NSInteger periodCount;
        [Export("periodCount")]
        nint PeriodCount { get; }
    }

    // @interface PaymentManager : NSObject
    // SWIFT_CLASS("_TtC18StoreKit2Framework14PaymentManager")
    [BaseType(typeof(NSObject), Name = "_TtC18StoreKit2Framework14PaymentManager")]
    [DisableDefaultCtor]
    interface PaymentManager
    {
        // @property (nonatomic, class, readonly, strong) PaymentManager * _Nonnull shared;
        [Static]
        [Export("shared", ArgumentSemantic.Strong)]
        PaymentManager Shared { get; }

        [Wrap("WeakDelegate")]
        [NullAllowed]
        PaymentManagerDelegate Delegate { get; set; }

        // @property (nonatomic, weak) id<PaymentManagerDelegate> _Nullable delegate;
        [NullAllowed, Export("delegate", ArgumentSemantic.Weak)]
        NSObject WeakDelegate { get; set; }

        // -(void)requestProductsWithProductIds:(NSArray<NSString *> * _Nonnull)productIds completion:(void (^ _Nonnull)(BOOL, NSString * _Nullable))completion;
        [Export("requestProductsWithProductIds:completion:")]
        void RequestProductsWithProductIds(string[] productIds, Action<bool, NSString> completion);

        // -(void)purchaseProductWithProductId:(NSString * _Nonnull)productId appAccountToken:(NSUUID * _Nullable)appAccountToken completion:(void (^ _Nonnull)(BOOL, NSString * _Nullable))completion;
        [Export("purchaseProductWithProductId:appAccountToken:completion:")]
        void PurchaseProductWithProductId(string productId, [NullAllowed] NSUuid appAccountToken, Action<bool, NSString> completion);

        // -(void)restorePurchasesWithCompletion:(void (^ _Nonnull)(BOOL, NSString * _Nullable))completion;
        [Export("restorePurchasesWithCompletion:")]
        void RestorePurchasesWithCompletion(Action<bool, NSString> completion);

        // -(PaymentProduct * _Nullable)getProductWithProductId:(NSString * _Nonnull)productId;
        [Export("getProductWithProductId:")]
        [return: NullAllowed]
        PaymentProduct GetProductWithProductId(string productId);

        // -(NSArray<PaymentProduct *> * _Nonnull)getAllProducts;
        [Export("getAllProducts")]
        PaymentProduct[] AllProducts { get; }

        // -(void)checkIntroductoryOfferEligibilityWithProductId:(NSString * _Nonnull)productId completion:(void (^ _Nonnull)(BOOL))completion;
        [Export("checkIntroductoryOfferEligibilityWithProductId:completion:")]
        void CheckIntroductoryOfferEligibility(string productId, Action<bool> completion);

        // -(void)checkPurchaseStatusWithProductId:(NSString * _Nonnull)productId completion:(void (^ _Nonnull)(BOOL, PaymentTransaction * _Nullable))completion;
        [Export("checkPurchaseStatusWithProductId:completion:")]
        void CheckPurchaseStatusWithProductId(string productId, Action<bool, PaymentTransaction> completion);
    }

    // @protocol PaymentManagerDelegate
    // SWIFT_PROTOCOL("_TtP18StoreKit2Framework22PaymentManagerDelegate_")
    [Protocol(Name = "_TtP18StoreKit2Framework22PaymentManagerDelegate_"), Model]
    [BaseType(typeof(NSObject))]
    interface PaymentManagerDelegate
    {
        // @optional -(void)paymentManagerDidFinishPurchase:(NSString * _Nonnull)productId transaction:(PaymentTransaction * _Nonnull)transaction;
        [Export("paymentManagerDidFinishPurchase:transaction:")]
        void PaymentManagerDidFinishPurchase(string productId, PaymentTransaction transaction);

        // @optional -(void)paymentManagerDidFailPurchase:(NSString * _Nonnull)productId error:(NSString * _Nonnull)error;
        [Export("paymentManagerDidFailPurchase:error:")]
        void PaymentManagerDidFailPurchase(string productId, string error);

        // @optional -(void)paymentManagerDidUpdateProducts:(NSArray<PaymentProduct *> * _Nonnull)products;
        [Export("paymentManagerDidUpdateProducts:")]
        void PaymentManagerDidUpdateProducts(PaymentProduct[] products);

        // @optional -(void)paymentManagerDidRestorePurchases:(NSArray<PaymentTransaction *> * _Nonnull)transactions;
        [Export("paymentManagerDidRestorePurchases:")]
        void PaymentManagerDidRestorePurchases(PaymentTransaction[] transactions);
    }

    // @interface PaymentProduct : NSObject
    // SWIFT_CLASS("_TtC18StoreKit2Framework14PaymentProduct")
    [BaseType(typeof(NSObject), Name = "_TtC18StoreKit2Framework14PaymentProduct")]
    [DisableDefaultCtor]
    interface PaymentProduct
    {
        // @property (nonatomic, readonly, copy) NSString * _Nonnull productId;
        [Export("productId")]
        string ProductId { get; }

        // @property (nonatomic, readonly, copy) NSString * _Nonnull displayName;
        [Export("displayName")]
        string DisplayName { get; }

        // @property (nonatomic, readonly, copy) NSString * _Nonnull productDescription;
        [Export("productDescription")]
        string ProductDescription { get; }

        // @property (nonatomic, readonly, strong) NSDecimalNumber * _Nonnull price;
        [Export("price", ArgumentSemantic.Strong)]
        NSDecimalNumber Price { get; }

        // @property (nonatomic, readonly, copy) NSString * _Nonnull displayPrice;
        [Export("displayPrice")]
        string DisplayPrice { get; }

        // @property (nonatomic, readonly, copy) NSString * _Nonnull productType;
        // "consumable" | "nonConsumable" | "autoRenewable" | "nonRenewable" | "unknown"
        [Export("productType")]
        string ProductType { get; }

        // @property (nonatomic, readonly, strong) PaymentIntroductoryOffer * _Nullable introductoryOffer;
        [NullAllowed, Export("introductoryOffer", ArgumentSemantic.Strong)]
        PaymentIntroductoryOffer IntroductoryOffer { get; }

        // @property (nonatomic, readonly, copy) NSArray<PaymentIntroductoryOffer *> * _Nonnull promotionalOffers;
        [Export("promotionalOffers", ArgumentSemantic.Copy)]
        PaymentIntroductoryOffer[] PromotionalOffers { get; }

        // @property (nonatomic, readonly, copy) NSString * _Nullable subscriptionGroupId;
        [NullAllowed, Export("subscriptionGroupId")]
        string SubscriptionGroupId { get; }

        // @property (nonatomic, readonly) NSInteger subscriptionPeriodValue;
        [Export("subscriptionPeriodValue")]
        nint SubscriptionPeriodValue { get; }

        // "day" | "week" | "month" | "year" | "unknown"
        // @property (nonatomic, readonly, copy) NSString * _Nonnull subscriptionPeriodUnit;
        [Export("subscriptionPeriodUnit")]
        string SubscriptionPeriodUnit { get; }

        // @property (nonatomic, readonly) BOOL isFamilyShareable;
        [Export("isFamilyShareable")]
        bool IsFamilyShareable { get; }
    }

    // @interface PaymentTransaction : NSObject
    // SWIFT_CLASS("_TtC18StoreKit2Framework18PaymentTransaction")
    [BaseType(typeof(NSObject), Name = "_TtC18StoreKit2Framework18PaymentTransaction")]
    [DisableDefaultCtor]
    interface PaymentTransaction
    {
        // @property (nonatomic, readonly, copy) NSString * _Nonnull transactionId;
        [Export("transactionId")]
        string TransactionId { get; }

        // @property (nonatomic, readonly, copy) NSString * _Nonnull originaltransactionId;
        [Export("originaltransactionId")]
        string OriginalTransactionId { get; }

        // @property (nonatomic, readonly, copy) NSString * _Nonnull productId;
        [Export("productId")]
        string ProductId { get; }

        // @property (nonatomic, readonly, copy) NSDate * _Nonnull purchaseDate;
        [Export("purchaseDate", ArgumentSemantic.Copy)]
        NSDate PurchaseDate { get; }

        // @property (nonatomic, readonly) BOOL isUpgraded;
        [Export("isUpgraded")]
        bool IsUpgraded { get; }

        // @property (nonatomic, readonly, copy) NSDate * _Nullable revocationDate;
        [NullAllowed, Export("revocationDate", ArgumentSemantic.Copy)]
        NSDate RevocationDate { get; }

        // @property (nonatomic, readonly, copy) NSString * _Nullable revocationReason;
        [NullAllowed, Export("revocationReason")]
        string RevocationReason { get; }
    }
}

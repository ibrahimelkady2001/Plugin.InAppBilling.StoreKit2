//
//  StoreKit2Framework.swift
//  StoreKit2Framework
//
//  Created by Yuting Li on 2025/5/28.
//

import Foundation
import StoreKit

@available(iOS 15.0, *)
@objc public protocol PaymentManagerDelegate: AnyObject {
    @objc optional func paymentManagerDidFinishPurchase(_ productId: String, transaction: PaymentTransaction)
    @objc optional func paymentManagerDidFailPurchase(_ productId: String, error: String)
    @objc optional func paymentManagerDidUpdateProducts(_ products: [PaymentProduct])
    @objc optional func paymentManagerDidRestorePurchases(_ transactions: [PaymentTransaction])
}

@available(iOS 15.0, *)
@objc public class PaymentIntroductoryOffer: NSObject {
    @objc public let id: String
    @objc public let displayPrice: String
    @objc public let price: NSDecimalNumber
    /// "freeTrial" | "payAsYouGo" | "payUpFront" | "unknown"
    @objc public let paymentMode: String
    /// Number of period units (e.g. 3 for "3 months")
    @objc public let periodValue: Int
    /// "day" | "week" | "month" | "year"
    @objc public let periodUnit: String
    /// How many periods the offer lasts
    @objc public let periodCount: Int

    public init(offer: Product.SubscriptionOffer) {
        self.id          = offer.id ?? ""
        self.displayPrice = offer.displayPrice
        self.price       = NSDecimalNumber(decimal: offer.price)
        self.periodValue = offer.period.value
        self.periodCount = offer.periodCount

        switch offer.paymentMode {
        case .freeTrial:  self.paymentMode = "freeTrial"
        case .payAsYouGo: self.paymentMode = "payAsYouGo"
        case .payUpFront: self.paymentMode = "payUpFront"
        default:          self.paymentMode = "unknown"
        }

        switch offer.period.unit {
        case .day:   self.periodUnit = "day"
        case .week:  self.periodUnit = "week"
        case .month: self.periodUnit = "month"
        case .year:  self.periodUnit = "year"
        default:     self.periodUnit = "unknown"
        }

        super.init()
    }
}

@available(iOS 15.0, *)
@objc public class PaymentProduct: NSObject {
    @objc public let productId: String
    @objc public let displayName: String
    @objc public let productDescription: String
    @objc public let price: NSDecimalNumber
    @objc public let displayPrice: String
    @objc public let productType: String
    @objc public let introductoryOffer: PaymentIntroductoryOffer?
    @objc public let promotionalOffers: [PaymentIntroductoryOffer]
    @objc public let subscriptionGroupId: String?
    @objc public let subscriptionPeriodValue: Int
    @objc public let subscriptionPeriodUnit: String
    @objc public let isFamilyShareable: Bool

    public init(product: Product) {
        self.productId          = product.id
        self.displayName        = product.displayName
        self.productDescription = product.description
        self.price              = NSDecimalNumber(decimal: product.price)
        self.displayPrice       = product.displayPrice

        switch product.type {
        case .consumable:    self.productType = "consumable"
        case .nonConsumable: self.productType = "nonConsumable"
        case .autoRenewable: self.productType = "autoRenewable"
        case .nonRenewable:  self.productType = "nonRenewable"
        default:             self.productType = "unknown"
        }

        self.isFamilyShareable = product.isFamilyShareable

        if let sub = product.subscription {
            self.subscriptionGroupId     = sub.subscriptionGroupID
            self.subscriptionPeriodValue = sub.subscriptionPeriod.value
            switch sub.subscriptionPeriod.unit {
            case .day:   self.subscriptionPeriodUnit = "day"
            case .week:  self.subscriptionPeriodUnit = "week"
            case .month: self.subscriptionPeriodUnit = "month"
            case .year:  self.subscriptionPeriodUnit = "year"
            default:     self.subscriptionPeriodUnit = "unknown"
            }
            self.introductoryOffer  = sub.introductoryOffer.map { PaymentIntroductoryOffer(offer: $0) }
            self.promotionalOffers  = sub.promotionalOffers.map { PaymentIntroductoryOffer(offer: $0) }
        } else {
            self.subscriptionGroupId     = nil
            self.subscriptionPeriodValue = 0
            self.subscriptionPeriodUnit  = "unknown"
            self.introductoryOffer       = nil
            self.promotionalOffers       = []
        }

        super.init()
    }
}

@available(iOS 15.0, *)
@objc public class PaymentTransaction: NSObject {
    @objc public let transactionId: String
    @objc public let originaltransactionId: String
    @objc public let productId: String
    @objc public let purchaseDate: Date
    @objc public let isUpgraded: Bool
    @objc public let revocationDate: Date?
    @objc public let revocationReason: String?
    
    public init(transaction: Transaction) {
        self.originaltransactionId = String(transaction.originalID)
        self.transactionId = String(transaction.id)
        self.productId = transaction.productID
        self.purchaseDate = transaction.purchaseDate
        self.isUpgraded = transaction.isUpgraded
        self.revocationDate = transaction.revocationDate
        
        if let reason = transaction.revocationReason {
            switch reason {
            case .developerIssue:
                self.revocationReason = "developerIssue"
            case .other:
                self.revocationReason = "other"
            default:
                self.revocationReason = "unknown"
            }
        } else {
            self.revocationReason = nil
        }
        
        super.init()
    }
}

@available(iOS 15.0, *)
@objc public class PaymentManager: NSObject {
    @objc public static let shared: PaymentManager = PaymentManager()
    @objc public weak var delegate: PaymentManagerDelegate?
    
    private var products: [String: Product] = [:]
    private var transactionListener: Task<Void, Error>?
    
    private override init() {
        super.init()
        startTransactionListener()
    }
    
    deinit {
        transactionListener?.cancel()
    }
    
    // MARK: - Public Methods
    
    @objc public func requestProducts(productIds: [String], completion: @escaping (Bool, String?) -> Void) {
        Task {
            do {
                let storeProducts: [Product] = try await Product.products(for: Set(productIds))
                
                await MainActor.run {
                    for product: Product in storeProducts {
                        self.products[product.id] = product
                    }
                    
                    let paymentProducts: [PaymentProduct] = storeProducts.map { PaymentProduct(product: $0) }
                    self.delegate?.paymentManagerDidUpdateProducts?(paymentProducts)
                    completion(true, nil)
                }
            } catch {
                await MainActor.run {
                    completion(false, error.localizedDescription)
                }
            }
        }
    }
    
    @objc public func purchaseProduct(productId: String, appAccountToken: UUID? = nil, completion: @escaping (Bool, String?) -> Void) {
        guard let product = products[productId] else {
            completion(false, "Product not found: \(productId)")
            return
        }
        
        Task {
            do {
                var options: Set<Product.PurchaseOption> = []
                if let token = appAccountToken {
                    options.insert(.appAccountToken(token))
                }
                let result: Product.PurchaseResult = try await product.purchase(options: options)
                
                switch result {
                case .success(let verification):
                    switch verification {
                    case .verified(let transaction):
                        let paymentTransaction = PaymentTransaction(transaction: transaction)
                  
                        await MainActor.run {
                            self.delegate?.paymentManagerDidFinishPurchase?(productId, transaction: paymentTransaction)
                            completion(true, nil)
                        }
                        await transaction.finish()
                    case .unverified(_, let error):
                        await MainActor.run {
                            self.delegate?.paymentManagerDidFailPurchase?(productId, error: "Unverified transaction: \(error)")
                            completion(false, "Unverified transaction")
                        }
                    }
                case .userCancelled:
                    await MainActor.run {
                        self.delegate?.paymentManagerDidFailPurchase?(productId, error: "User cancelled")
                        completion(false, "User cancelled")
                    }
                case .pending:
                    await MainActor.run {
                        completion(false, "Purchase pending")
                    }
                @unknown default:
                    await MainActor.run {
                        completion(false, "Unknown result")
                    }
                }
            } catch {
                await MainActor.run {
                    self.delegate?.paymentManagerDidFailPurchase?(productId, error: error.localizedDescription)
                    completion(false, error.localizedDescription)
                }
            }
        }
    }
    
    @objc public func restorePurchases(completion: @escaping (Bool, String?) -> Void) {
        Task {
            do {
                try await AppStore.sync()
                
                let restoredTransactions = await collectCurrentEntitlements()
                
                await MainActor.run {
                    self.delegate?.paymentManagerDidRestorePurchases?(restoredTransactions)
                    completion(true, nil)
                }
            } catch {
                await MainActor.run {
                    completion(false, error.localizedDescription)
                }
            }
        }
    }
    
    @objc public func getProduct(productId: String) -> PaymentProduct? {
        guard let product = products[productId] else { return nil }
        return PaymentProduct(product: product)
    }
    
    @objc public func getAllProducts() -> [PaymentProduct] {
        return products.values.map { PaymentProduct(product: $0) }
    }
    
    /// Returns true if the user has never redeemed an introductory offer for this product's subscription group.
    @objc public func checkIntroductoryOfferEligibility(productId: String, completion: @escaping (Bool) -> Void) {
        guard let product = products[productId], let subscription = product.subscription else {
            completion(false)
            return
        }
        Task {
            let eligible = await subscription.isEligibleForIntroOffer
            await MainActor.run { completion(eligible) }
        }
    }

    @objc public func checkPurchaseStatus(productId: String, completion: @escaping (Bool, PaymentTransaction?) -> Void) {
        Task {
            let foundTransaction = await findCurrentEntitlement(for: productId)
            
            await MainActor.run {
                if let transaction = foundTransaction {
                    completion(true, transaction)
                } else {
                    completion(false, nil)
                }
            }
        }
    }
    
    // MARK: - Private Methods
    
    private func collectCurrentEntitlements() async -> [PaymentTransaction] {
        var transactions: [PaymentTransaction] = []
        
        for await result in Transaction.currentEntitlements {
            switch result {
            case .verified(let transaction):
               
                transactions.append(PaymentTransaction(transaction: transaction))
            case .unverified(_, _):
                continue
            }
        }
        
        return transactions
    }
    
    private func findCurrentEntitlement(for productId: String) async -> PaymentTransaction? {
        for await result in Transaction.currentEntitlements {
            switch result {
            case .verified(let transaction):
                if transaction.productID == productId {
                    return PaymentTransaction(transaction: transaction)
                }
            case .unverified(_, _):
                continue
            }
        }
        return nil
    }
    
    private func startTransactionListener() {
        transactionListener = Task(priority: .background) {
            for await result in Transaction.updates {
                switch result {
                case .verified(let transaction):
                    let paymentTransaction = PaymentTransaction(transaction: transaction)
                    await MainActor.run {
                        self.delegate?.paymentManagerDidFinishPurchase?(transaction.productID, transaction: paymentTransaction)
                    }
                    await transaction.finish()
                case .unverified(_, let error):
                    await MainActor.run {
                        self.delegate?.paymentManagerDidFailPurchase?("unknown", error: "Unverified transaction: \(error)")
                    }
                }
            }
        }
    }
}

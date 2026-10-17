import { api } from '../../services/api';

export interface PublicPlanOffer {
  offerId: string;
  planVersionId: string;
  planKey: string;
  planName: string;
  categoryKey: string;
  categoryName: string;
  billingIntervalMonths: number;
  priceCents: number;
  signupFeeCents: number;
  marketingReferencePriceCents?: number | null;
  subaccountLimit: number;
  monthlyPublicationLimit: number;
  version: number;
}

export interface BillingCheckoutResponse {
  subscriptionId: string;
  planVersionId: string;
  checkoutUrl: string;
  providerStatus: string;
}

export interface BillingEntitlements {
  subscriptionId: string;
  planVersionId: string;
  planKey: string;
  planName: string;
  categoryKey: string;
  categoryName: string;
  subscriptionStatus: string;
  accessAllowed: boolean;
  subaccountLimit: number;
  monthlyPublicationLimit: number;
  usedPublications: number;
  usageWindowStart: string;
  usageWindowEnd: string;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  gracePeriodEndsAt?: string | null;
  cancelAtPeriodEnd: boolean;
  pendingPlanVersionId?: string | null;
}

export interface BillingPlanChangeResponse {
  planVersionId?: string | null;
  requiresAuthorization: boolean;
  checkoutUrl?: string | null;
  effectiveAt?: string | null;
  providerStatus?: string | null;
}

export async function listPublicPlanOffers() {
  const { data } = await api.get<PublicPlanOffer[]>('/billing/catalog');
  return data;
}

export async function createBillingCheckout(
  planVersionId: string,
  resumeAfterSignupFee = false,
  signupFeePaymentId?: string | null,
) {
  const { data } = await api.post<BillingCheckoutResponse>('/billing/checkout', {
    planVersionId,
    resumeAfterSignupFee,
    signupFeePaymentId,
  });

  return data;
}

export async function getBillingEntitlements() {
  const { data } = await api.get<BillingEntitlements>('/billing/me');
  return data;
}

export async function changeBillingPlan(planVersionId: string) {
  const { data } = await api.put<BillingPlanChangeResponse>('/billing/plan', {
    planVersionId,
  });

  return data;
}
export async function cancelBillingSubscription() {
  const { data } = await api.post<BillingPlanChangeResponse>('/billing/cancellation');
  return data;
}

export async function reactivateBillingSubscription() {
  const { data } = await api.post<BillingPlanChangeResponse>('/billing/reactivation');
  return data;
}

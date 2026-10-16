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

export async function listPublicPlanOffers() {
  const { data } = await api.get<PublicPlanOffer[]>('/billing/catalog');
  return data;
}

export async function createBillingCheckout(planVersionId: string) {
  const { data } = await api.post<BillingCheckoutResponse>('/billing/checkout', {
    planVersionId,
  });

  return data;
}

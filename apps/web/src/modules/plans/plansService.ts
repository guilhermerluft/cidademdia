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

export async function listPublicPlanOffers() {
  const { data } = await api.get<PublicPlanOffer[]>('/billing/catalog');
  return data;
}

export interface AdminPage<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
}

export interface AdminOverview {
  users: number;
  activeUsers: number;
  suspendedUsers: number;
  blockedUsers: number;
  masters: number;
  subaccounts: number;
  institutions: number;
  occurrences: number;
  posts: number;
  activeSubscriptions: number;
  payments: number;
  generatedAt: string;
}

export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  status: string;
  roles: string[];
  emailConfirmed: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AdminInstitution {
  id: string;
  name: string;
  slug: string;
  type: string;
  scopeLevel: string;
  status: string;
  officialEmail: string | null;
  stateCode: string | null;
  representatives: number;
  memberships: number;
  createdAt: string;
  updatedAt: string;
}

export interface AdminOccurrence {
  id: string;
  publicCode: string;
  authorUserId: string;
  title: string;
  status: string;
  addressText: string;
  stateCode: string | null;
  createdAt: string;
  updatedAt: string;
  closedAt: string | null;
  cancelledAt: string | null;
}

export interface AdminPost {
  id: string;
  publisherUserId: string;
  masterUserId: string | null;
  type: string;
  status: string;
  title: string | null;
  mediaCount: number;
  placementCount: number;
  createdAt: string;
  updatedAt: string;
  publishedAt: string | null;
  archivedAt: string | null;
}

export interface AdminPlan {
  planVersionId: string;
  planKey: string;
  planName: string;
  offerKey: string;
  billingCategoryKey: string;
  billingCategoryName: string;
  billingIntervalMonths: number;
  version: number;
  priceCents: number;
  signupFeeCents: number;
  subaccountLimit: number;
  monthlyPublicationLimit: number;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface AdminSubscription {
  id: string;
  masterUserId: string;
  masterEmail: string;
  masterDisplayName: string;
  status: string;
  planKey: string;
  planName: string;
  offerKey: string;
  planVersion: number;
  subaccountLimit: number;
  monthlyPublicationLimit: number;
  currentPublicationCount: number;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  cancelAtPeriodEnd: boolean;
  pastDueAt: string | null;
  gracePeriodEndsAt: string | null;
  canceledAt: string | null;
}

export interface AdminPayment {
  id: string;
  subscriptionId: string;
  masterUserId: string;
  masterEmail: string;
  provider: string;
  providerPaymentId: string;
  amountCents: number;
  currency: string;
  status: string;
  statusDetail: string | null;
  approvedAt: string | null;
  createdAt: string;
}

export interface AdminBillingSnapshot {
  plans: AdminPlan[];
  subscriptions: AdminPage<AdminSubscription>;
  payments: AdminPage<AdminPayment>;
}

export interface AdminAuditLog {
  id: string;
  actorUserId: string;
  actorEmail: string | null;
  actorDisplayName: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  previousValue: string | null;
  newValue: string | null;
  reason: string;
  occurredAt: string;
}

export type AdminTab =
  | 'users'
  | 'institutions'
  | 'occurrences'
  | 'posts'
  | 'billing'
  | 'audit';

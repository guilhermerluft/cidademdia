export type InstitutionType =
  | 'CITY_HALL'
  | 'CITY_COUNCIL'
  | 'ASSEMBLY'
  | 'PUBLIC_AGENCY'
  | 'PUBLIC_SERVICE'
  | 'OTHER';

export type InstitutionScopeLevel =
  | 'MUNICIPAL'
  | 'STATE'
  | 'FEDERAL'
  | 'REGIONAL'
  | 'OTHER';

export type RepresentativeProfileStatus =
  | 'NOT_REGISTERED'
  | 'INVITED'
  | 'ACTIVE'
  | 'INACTIVE';

export interface InstitutionJurisdictionItem {
  id: string;
  jurisdictionType: 'CITY' | 'STATE' | 'CUSTOM_AREA';
  cityId?: string | null;
  stateCode?: string | null;
  customAreaLabel?: string | null;
}

export interface InstitutionRepresentativeItem {
  id: string;
  institutionId: string;
  name: string;
  slug: string;
  publicRole: string;
  officialEmail?: string | null;
  photoMediaId?: string | null;
  mandateStart?: string | null;
  mandateEnd?: string | null;
  accountId?: string | null;
  profileStatus: RepresentativeProfileStatus;
  displayOrder: number;
}

export interface InstitutionItem {
  id: string;
  name: string;
  slug: string;
  type: InstitutionType;
  scopeLevel: InstitutionScopeLevel;
  officialEmail?: string | null;
  officialDomain?: string | null;
  description?: string | null;
  logoMediaId?: string | null;
  cityId?: string | null;
  stateCode?: string | null;
  status: 'ACTIVE' | 'INACTIVE';
  jurisdictions: InstitutionJurisdictionItem[];
  representatives: InstitutionRepresentativeItem[];
}

export interface InstitutionDirectoryPage {
  items: InstitutionItem[];
  page: number;
  pageSize: number;
  totalItems: number;
}

export interface MasterDirectoryInstitutionItem {
  institutionId: string;
  name: string;
  type: InstitutionType;
  scopeLevel: InstitutionScopeLevel;
  stateCode?: string | null;
  publicRole?: string | null;
}

export interface MasterDirectoryItem {
  id: string;
  displayName: string;
  avatarMediaId?: string | null;
  institutions: MasterDirectoryInstitutionItem[];
}

export interface MasterDirectoryPage {
  items: MasterDirectoryItem[];
  page: number;
  pageSize: number;
  totalItems: number;
}

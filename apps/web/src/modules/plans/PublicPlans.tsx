import { useEffect, useMemo, useState } from 'react';
import { Brand, Button } from '../../components/ui';
import { listPublicPlanOffers, type PublicPlanOffer } from './plansService';

interface PublicPlansProps {
  onHome: () => void;
  onLogin: () => void;
  onRegister: () => void;
  embedded?: boolean;
  onSelectOffer?: (offer: PublicPlanOffer) => void;
  onContact?: () => void;
}

type BillingCycle = 1 | 3 | 6;
type PlanTone = 'blue' | 'green' | 'sky';

interface PlanDefinition {
  id: 'individual' | 'master-5' | 'master-10';
  title: string;
  eyebrow: string;
  icon: string;
  tone: PlanTone;
  fallbackSubaccounts: number;
  description: string;
}

const PLAN_DEFINITIONS: PlanDefinition[] = [
  {
    id: 'individual',
    title: 'Individual',
    eyebrow: 'Comece simples',
    icon: 'fa-user',
    tone: 'blue',
    fallbackSubaccounts: 0,
    description: 'Para quem precisa acompanhar ocorrências e publicar com uma gestão direta.',
  },
  {
    id: 'master-5',
    title: 'Master 5',
    eyebrow: 'Mais escolhido',
    icon: 'fa-building-user',
    tone: 'green',
    fallbackSubaccounts: 5,
    description: 'Para equipes que precisam distribuir acessos sem perder controle da operação.',
  },
  {
    id: 'master-10',
    title: 'Master 10',
    eyebrow: 'Mais capacidade',
    icon: 'fa-people-group',
    tone: 'sky',
    fallbackSubaccounts: 10,
    description: 'Para estruturas maiores, com mais pessoas, publicações e volume de atendimento.',
  },
];

const REGULAR_CYCLES: { months: BillingCycle; label: string; hint: string }[] = [
  { months: 1, label: 'Mensal', hint: 'Flexível' },
  { months: 3, label: 'Trimestral', hint: '3 meses' },
  { months: 6, label: 'Semestral', hint: '6 meses' },
];

const TRUST_ITEMS = [
  { icon: 'fa-clipboard-list', title: 'Ocorrências', text: 'Acompanhe demandas compartilhadas.' },
  { icon: 'fa-users-gear', title: 'Subcontas', text: 'Distribua acessos com controle.' },
  { icon: 'fa-bell', title: 'Notificações', text: 'Receba movimentações importantes.' },
  { icon: 'fa-photo-film', title: 'Publicações', text: 'Use a franquia mensal do plano.' },
] as const;

function normalize(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, ' ')
    .trim();
}

function formatMoney(valueInCents: number) {
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
    maximumFractionDigits: 2,
  }).format(valueInCents / 100);
}

function offerMatchesPlan(definition: PlanDefinition, offer: PublicPlanOffer) {
  const searchable = normalize(`${offer.planKey} ${offer.planName}`);

  if (definition.id === 'individual') {
    return searchable.includes('individual') || offer.subaccountLimit === 0;
  }

  if (definition.id === 'master-5') {
    return searchable.includes('master 5') || offer.subaccountLimit === 5;
  }

  return searchable.includes('master 10') || offer.subaccountLimit === 10;
}

function getPlanOffers(definition: PlanDefinition, offers: PublicPlanOffer[]) {
  return offers
    .filter((offer) => offerMatchesPlan(definition, offer))
    .sort((left, right) => left.billingIntervalMonths - right.billingIntervalMonths);
}

function getOffer(offers: PublicPlanOffer[], months: number) {
  return offers.find((offer) => offer.billingIntervalMonths === months);
}

function getReferenceAnnualPrice(annual: PublicPlanOffer, monthly?: PublicPlanOffer) {
  if (annual.marketingReferencePriceCents && annual.marketingReferencePriceCents > annual.priceCents) {
    return annual.marketingReferencePriceCents;
  }

  if (monthly && monthly.priceCents * 12 > annual.priceCents) {
    return monthly.priceCents * 12;
  }

  return null;
}

function PublicPlansHeader({ onHome, onLogin, onRegister }: Pick<PublicPlansProps, 'onHome' | 'onLogin' | 'onRegister'>) {
  return (
    <header className="plans-page__header">
      <div className="plans-page__header-inner">
        <button className="plans-page__brand-button" type="button" onClick={onHome} aria-label="Ir para o início">
          <Brand className="plans-page__brand" />
        </button>

        <nav className="plans-page__nav" aria-label="Navegação principal">
          <button type="button" onClick={onHome}>
            <i className="fa-solid fa-house" aria-hidden="true" />
            Início
          </button>
          <a className="plans-page__nav-active" href="/planos" aria-current="page">
            <i className="fa-solid fa-layer-group" aria-hidden="true" />
            Planos
          </a>
        </nav>

        <div className="plans-page__header-actions">
          <Button variant="ghost" onClick={onLogin}>Entrar</Button>
          <Button onClick={onRegister}>Criar conta</Button>
        </div>
      </div>
    </header>
  );
}

function BillingSelector({
  selected,
  available,
  onChange,
}: {
  selected: BillingCycle;
  available: Set<number>;
  onChange: (cycle: BillingCycle) => void;
}) {
  return (
    <div className="pricing-cycle" aria-label="Periodicidade dos planos">
      <span className="pricing-cycle__label">Periodicidade</span>
      <div className="pricing-cycle__options" role="group" aria-label="Escolha a periodicidade">
        {REGULAR_CYCLES.map((cycle) => {
          const active = selected === cycle.months;
          const enabled = available.size === 0 || available.has(cycle.months);

          return (
            <button
              key={cycle.months}
              type="button"
              className={active ? 'is-active' : ''}
              aria-pressed={active}
              disabled={!enabled}
              onClick={() => onChange(cycle.months)}
            >
              <strong>{cycle.label}</strong>
              <small>{cycle.hint}</small>
            </button>
          );
        })}
      </div>
      <span className="pricing-cycle__annual-note">
        <i className="fa-solid fa-star" aria-hidden="true" />
        O anual tem condição especial abaixo.
      </span>
    </div>
  );
}

function PlanCard({
  definition,
  offers,
  cycle,
  onSelectOffer,
}: {
  definition: PlanDefinition;
  offers: PublicPlanOffer[];
  cycle: BillingCycle;
  onSelectOffer?: (offer: PublicPlanOffer) => void;
}) {
  const offer = getOffer(offers, cycle);
  const publicationLimit = offer?.monthlyPublicationLimit ?? offers[0]?.monthlyPublicationLimit ?? 0;
  const subaccountLimit = offer?.subaccountLimit ?? offers[0]?.subaccountLimit ?? definition.fallbackSubaccounts;
  const signupFee = offer?.signupFeeCents ?? offers[0]?.signupFeeCents ?? 0;
  const equivalentMonthly = offer && cycle > 1 ? Math.round(offer.priceCents / cycle) : null;

  return (
    <article className={`pricing-card pricing-card--${definition.tone}`}>
      <div className="pricing-card__accent" aria-hidden="true" />
      <div className="pricing-card__top">
        <span className="pricing-card__icon" aria-hidden="true">
          <i className={`fa-solid ${definition.icon}`} />
        </span>
        <span className="pricing-card__eyebrow">{definition.eyebrow}</span>
      </div>

      <div className="pricing-card__title">
        <span>Plano</span>
        <h2>{definition.title}</h2>
        <p>{definition.description}</p>
      </div>

      <div className="pricing-card__metrics">
        <div>
          <i className="fa-solid fa-table-columns" aria-hidden="true" />
          <span><strong>1</strong> Painel Master</span>
        </div>
        <div>
          <i className="fa-solid fa-user-group" aria-hidden="true" />
          <span><strong>{subaccountLimit}</strong> subconta{subaccountLimit === 1 ? '' : 's'}</span>
        </div>
        <div>
          <i className="fa-solid fa-bullhorn" aria-hidden="true" />
          <span><strong>{publicationLimit || '—'}</strong> postagem{publicationLimit === 1 ? '' : 'ens'}/mês</span>
        </div>
      </div>

      <div className="pricing-card__price-block">
        {offer ? (
          <>
            <span className="pricing-card__cycle-label">
              {cycle === 1 ? 'Mensal' : cycle === 3 ? 'Trimestral' : 'Semestral'}
            </span>
            <div className="pricing-card__price">
              <strong>{formatMoney(offer.priceCents)}</strong>
              <span>{cycle === 1 ? '/mês' : `/${cycle} meses`}</span>
            </div>
            {equivalentMonthly && (
              <small>Equivale a {formatMoney(equivalentMonthly)} por mês.</small>
            )}
            <div className="pricing-card__signup">
              <i className="fa-solid fa-ticket" aria-hidden="true" />
              {signupFee > 0 ? `Adesão única de ${formatMoney(signupFee)}` : 'Sem taxa de adesão'}
            </div>
            <button type="button" onClick={() => onSelectOffer?.(offer)}>
              Escolher {definition.title}
              <i className="fa-solid fa-arrow-right" aria-hidden="true" />
            </button>
          </>
        ) : (
          <div className="pricing-card__unavailable">
            <i className="fa-solid fa-clock" aria-hidden="true" />
            <strong>Condição indisponível</strong>
            <span>Este ciclo não está publicado para o plano atual.</span>
          </div>
        )}
      </div>
    </article>
  );
}

function AnnualSpotlight({ offers, onSelectOffer }: { offers: PublicPlanOffer[]; onSelectOffer?: (offer: PublicPlanOffer) => void }) {
  const annualOffers = PLAN_DEFINITIONS
    .map((definition) => {
      const planOffers = getPlanOffers(definition, offers);
      const annual = getOffer(planOffers, 12);
      const monthly = getOffer(planOffers, 1);
      return annual ? { definition, offer: annual, reference: getReferenceAnnualPrice(annual, monthly) } : null;
    })
    .filter((item): item is { definition: PlanDefinition; offer: PublicPlanOffer; reference: number | null } => Boolean(item));

  return (
    <section className="annual-spotlight" aria-labelledby="annual-spotlight-title">
      <div className="annual-spotlight__copy">
        <span className="annual-spotlight__badge">
          <i className="fa-solid fa-star" aria-hidden="true" />
          MELHOR CONDIÇÃO
        </span>
        <span className="annual-spotlight__eyebrow">Ouro Anual</span>
        <h2 id="annual-spotlight-title">Um ano inteiro com menos renovações e mais previsibilidade.</h2>
        <p>As condições anuais são carregadas do catálogo vigente e ficam separadas para facilitar a comparação.</p>

        <div className="annual-spotlight__benefits">
          <span><i className="fa-solid fa-calendar-check" aria-hidden="true" /> 12 meses</span>
          <span><i className="fa-solid fa-piggy-bank" aria-hidden="true" /> Melhor relação anual</span>
          <span><i className="fa-solid fa-shield-halved" aria-hidden="true" /> Contratação segura</span>
        </div>
      </div>

      <div className="annual-spotlight__offers">
        {annualOffers.length > 0 ? annualOffers.map(({ definition, offer, reference }) => (
          <article key={offer.offerId}>
            <div className="annual-spotlight__offer-name">
              <span>{definition.title}</span>
              <small>{offer.monthlyPublicationLimit} postagem{offer.monthlyPublicationLimit === 1 ? '' : 'ens'}/mês</small>
            </div>
            <div className="annual-spotlight__offer-price">
              {reference && <del>{formatMoney(reference)}</del>}
              <strong>{formatMoney(offer.priceCents)}</strong>
              <small>12 meses</small>
            </div>
            <button type="button" onClick={() => onSelectOffer?.(offer)} aria-label={`Escolher ${definition.title} anual`}>
              <i className="fa-solid fa-arrow-right" aria-hidden="true" />
            </button>
          </article>
        )) : (
          <div className="annual-spotlight__empty">
            <i className="fa-solid fa-clock" aria-hidden="true" />
            <span>As ofertas anuais não estão publicadas no catálogo neste momento.</span>
          </div>
        )}
      </div>
    </section>
  );
}

function PlansContent({
  offers,
  loading,
  unavailable,
  onSelectOffer,
  onContact,
}: {
  offers: PublicPlanOffer[];
  loading: boolean;
  unavailable: boolean;
  onSelectOffer?: (offer: PublicPlanOffer) => void;
  onContact?: () => void;
}) {
  const [cycle, setCycle] = useState<BillingCycle>(1);

  const groupedPlans = useMemo(
    () => PLAN_DEFINITIONS.map((definition) => ({ definition, offers: getPlanOffers(definition, offers) })),
    [offers],
  );

  const availableCycles = useMemo(
    () => new Set(offers.filter((offer) => offer.billingIntervalMonths !== 12).map((offer) => offer.billingIntervalMonths)),
    [offers],
  );

  useEffect(() => {
    if (offers.length === 0 || availableCycles.has(cycle)) return;
    const first = REGULAR_CYCLES.find((item) => availableCycles.has(item.months));
    if (first) setCycle(first.months);
  }, [availableCycles, cycle, offers.length]);

  return (
    <div className="pricing-page">
      <section className="pricing-page__intro">
        <div>
          <span className="pricing-page__kicker">Planos CIDADEMDIA</span>
          <h1>Escolha a estrutura que acompanha o ritmo da sua gestão.</h1>
        </div>
        <p>Compare capacidade, acessos e publicações. Os valores abaixo vêm diretamente do catálogo vigente.</p>
      </section>

      <BillingSelector selected={cycle} available={availableCycles} onChange={setCycle} />

      {loading && (
        <div className="pricing-state" aria-busy="true">
          <i className="fa-solid fa-spinner fa-spin" aria-hidden="true" />
          Carregando planos disponíveis...
        </div>
      )}

      {!loading && unavailable && (
        <div className="pricing-state pricing-state--error" role="alert">
          <i className="fa-solid fa-circle-exclamation" aria-hidden="true" />
          Não foi possível consultar o catálogo agora. Tente novamente em instantes.
        </div>
      )}

      {!loading && !unavailable && offers.length === 0 && (
        <div className="pricing-state">
          <i className="fa-solid fa-layer-group" aria-hidden="true" />
          Nenhuma oferta está publicada no catálogo neste momento.
        </div>
      )}

      {!loading && (
        <>
          <section className="pricing-grid" aria-label="Planos disponíveis">
            {groupedPlans.map(({ definition, offers: planOffers }) => (
              <PlanCard
                key={definition.id}
                definition={definition}
                offers={planOffers}
                cycle={cycle}
                onSelectOffer={onSelectOffer}
              />
            ))}
          </section>

          <AnnualSpotlight offers={offers} onSelectOffer={onSelectOffer} />
        </>
      )}

      <section className="pricing-trust" aria-label="Benefícios da plataforma">
        <div className="pricing-trust__items">
          {TRUST_ITEMS.map((item) => (
            <article key={item.title}>
              <span aria-hidden="true"><i className={`fa-solid ${item.icon}`} /></span>
              <div><strong>{item.title}</strong><small>{item.text}</small></div>
            </article>
          ))}
        </div>
        <div className="pricing-trust__contact">
          <div>
            <strong>Precisa de uma composição diferente?</strong>
            <span>Converse com a equipe sobre um plano personalizado.</span>
          </div>
          <button type="button" onClick={onContact}>Fale conosco</button>
        </div>
      </section>
    </div>
  );
}

export function PublicPlans({
  onHome,
  onLogin,
  onRegister,
  embedded = false,
  onSelectOffer,
  onContact,
}: PublicPlansProps) {
  const [offers, setOffers] = useState<PublicPlanOffer[]>([]);
  const [loading, setLoading] = useState(true);
  const [unavailable, setUnavailable] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setUnavailable(false);

    void listPublicPlanOffers()
      .then((items) => {
        if (!active) return;
        setOffers(items);
      })
      .catch(() => {
        if (!active) return;
        setOffers([]);
        setUnavailable(true);
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => { active = false; };
  }, []);

  const content = (
    <PlansContent
      offers={offers}
      loading={loading}
      unavailable={unavailable}
      onSelectOffer={onSelectOffer}
      onContact={onContact}
    />
  );

  if (embedded) {
    return <div className="plans-page plans-page--embedded">{content}</div>;
  }

  return (
    <div className="plans-page">
      <PublicPlansHeader onHome={onHome} onLogin={onLogin} onRegister={onRegister} />
      <main>{content}</main>
      <footer className="plans-page__footer">
        <div className="plans-page__footer-inner">
          <Brand compact />
          <span>CIDADEMDIA — conectando cidadãos e quem pode resolver.</span>
        </div>
      </footer>
    </div>
  );
}

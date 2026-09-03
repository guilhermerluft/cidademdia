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

type PlanTone = 'blue' | 'green' | 'sky';
type BillingCycle = 1 | 3 | 6 | 12;

interface PlanDefinition {
  id: 'individual' | 'master-5' | 'master-10';
  title: string;
  icon: string;
  tone: PlanTone;
  subaccountLimit: number;
  number: string;
  descriptor: string;
}

interface CycleDefinition {
  months: BillingCycle;
  label: string;
  shortLabel: string;
  hint: string;
}

const PLAN_DEFINITIONS: PlanDefinition[] = [
  {
    id: 'individual',
    title: 'Individual',
    icon: 'fa-user',
    tone: 'blue',
    subaccountLimit: 0,
    number: '01',
    descriptor: 'Gestão direta e objetiva para uma operação enxuta.',
  },
  {
    id: 'master-5',
    title: 'Master 5',
    icon: 'fa-building-user',
    tone: 'green',
    subaccountLimit: 5,
    number: '02',
    descriptor: 'Mais autonomia para distribuir acessos e organizar a equipe.',
  },
  {
    id: 'master-10',
    title: 'Master 10',
    icon: 'fa-people-group',
    tone: 'sky',
    subaccountLimit: 10,
    number: '03',
    descriptor: 'Estrutura ampliada para operações com mais pessoas e volume.',
  },
];

const CYCLE_DEFINITIONS: CycleDefinition[] = [
  { months: 1, label: 'Mensal', shortLabel: '1 mês', hint: 'Mais flexibilidade' },
  { months: 3, label: 'Trimestral', shortLabel: '3 meses', hint: 'Planejamento curto' },
  { months: 6, label: 'Semestral', shortLabel: '6 meses', hint: 'Mais previsibilidade' },
  { months: 12, label: 'Anual', shortLabel: '12 meses', hint: 'Melhor condição' },
];

const BENEFITS = [
  {
    icon: 'fa-clipboard-list',
    title: 'Acesso às ocorrências',
    description: 'Acompanhe as demandas compartilhadas com sua gestão.',
    tone: 'blue',
  },
  {
    icon: 'fa-users-gear',
    title: 'Gerencie subcontas',
    description: 'Distribua acessos conforme a estrutura contratada.',
    tone: 'green',
  },
  {
    icon: 'fa-bell',
    title: 'Receba notificações',
    description: 'Não perca movimentações importantes da operação.',
    tone: 'sky',
  },
  {
    icon: 'fa-photo-film',
    title: 'Postagens mensais',
    description: 'Publique de acordo com a franquia vigente do plano.',
    tone: 'yellow',
  },
] as const;

const TRUST_ITEMS = [
  {
    icon: 'fa-shield-halved',
    title: 'Pagamento e dados seguros',
    text: 'Fluxos protegidos e tratamento responsável das informações.',
  },
  {
    icon: 'fa-headset',
    title: 'Suporte dedicado',
    text: 'Apoio para orientar o uso dos recursos contratados.',
  },
  {
    icon: 'fa-ticket',
    title: 'Adesão transparente',
    text: 'A taxa aplicável aparece junto da condição escolhida.',
  },
  {
    icon: 'fa-microchip',
    title: 'Tecnologia conectada',
    text: 'Ocorrências, equipes e publicações em um só ambiente.',
  },
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

function getOfferByInterval(offers: PublicPlanOffer[], months: BillingCycle) {
  return offers.find((offer) => offer.billingIntervalMonths === months);
}

function getPublicationLimit(offers: PublicPlanOffer[]) {
  return offers.reduce((highest, offer) => Math.max(highest, offer.monthlyPublicationLimit), 0);
}

function getSignupFee(offers: PublicPlanOffer[]) {
  return offers.reduce((highest, offer) => Math.max(highest, offer.signupFeeCents), 0);
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

function getCycleDefinition(months: BillingCycle) {
  return CYCLE_DEFINITIONS.find((cycle) => cycle.months === months) ?? CYCLE_DEFINITIONS[0];
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

function CycleSelector({
  selectedCycle,
  availableCycles,
  onChange,
}: {
  selectedCycle: BillingCycle;
  availableCycles: Set<number>;
  onChange: (cycle: BillingCycle) => void;
}) {
  return (
    <div className="plans-v2__cycle-panel" aria-label="Ciclo de contratação">
      <div className="plans-v2__cycle-heading">
        <span>Ciclo de contratação</span>
        <strong>Escolha como prefere contratar</strong>
      </div>

      <div className="plans-v2__cycle-options" role="group" aria-label="Escolha o ciclo do plano">
        {CYCLE_DEFINITIONS.map((cycle) => {
          const selected = selectedCycle === cycle.months;
          const available = availableCycles.size === 0 || availableCycles.has(cycle.months);

          return (
            <button
              className={`plans-v2__cycle-option${selected ? ' plans-v2__cycle-option--active' : ''}${cycle.months === 12 ? ' plans-v2__cycle-option--annual' : ''}`}
              type="button"
              key={cycle.months}
              aria-pressed={selected}
              disabled={!available}
              onClick={() => onChange(cycle.months)}
            >
              <span>{cycle.label}</span>
              <small>{cycle.shortLabel}</small>
              {cycle.months === 12 && <em>Melhor condição</em>}
            </button>
          );
        })}
      </div>
    </div>
  );
}

function PlanCard({
  definition,
  offers,
  selectedCycle,
  onSelectOffer,
}: {
  definition: PlanDefinition;
  offers: PublicPlanOffer[];
  selectedCycle: BillingCycle;
  onSelectOffer?: (offer: PublicPlanOffer) => void;
}) {
  const selectedOffer = getOfferByInterval(offers, selectedCycle);
  const monthlyOffer = getOfferByInterval(offers, 1);
  const cycle = getCycleDefinition(selectedCycle);
  const publicationLimit = selectedOffer?.monthlyPublicationLimit ?? getPublicationLimit(offers);
  const subaccountLimit = selectedOffer?.subaccountLimit ?? offers[0]?.subaccountLimit ?? definition.subaccountLimit;
  const signupFee = selectedOffer?.signupFeeCents ?? getSignupFee(offers);
  const referencePrice = selectedOffer && selectedCycle === 12
    ? getReferenceAnnualPrice(selectedOffer, monthlyOffer)
    : null;
  const equivalentMonthly = selectedOffer && selectedCycle > 1
    ? Math.round(selectedOffer.priceCents / selectedCycle)
    : null;

  return (
    <article className={`plans-v2__plan-card plans-v2__plan-card--${definition.tone}`}>
      <div className="plans-v2__plan-rail" aria-hidden="true">
        <span>{definition.number}</span>
      </div>

      <div className="plans-v2__plan-topline">
        <span className="plans-v2__plan-icon" aria-hidden="true">
          <i className={`fa-solid ${definition.icon}`} />
        </span>
        <span className="plans-v2__publication-badge">
          <i className="fa-solid fa-bullhorn" aria-hidden="true" />
          {publicationLimit > 0
            ? `${publicationLimit} postagem${publicationLimit === 1 ? '' : 'ens'}/mês`
            : 'Postagens conforme catálogo'}
        </span>
      </div>

      <div className="plans-v2__plan-title">
        <span>Plano</span>
        <h3>{definition.title}</h3>
        <p>{definition.descriptor}</p>
      </div>

      <div className="plans-v2__plan-facts">
        <span>
          <i className="fa-solid fa-chart-line" aria-hidden="true" />
          1 Painel Master
        </span>
        <span>
          <i className="fa-solid fa-user-group" aria-hidden="true" />
          {subaccountLimit} subconta{subaccountLimit === 1 ? '' : 's'}
        </span>
      </div>

      <div className={`plans-v2__price-panel${selectedCycle === 12 ? ' plans-v2__price-panel--annual' : ''}`}>
        <div className="plans-v2__price-cycle">
          <span>{cycle.label}</span>
          <small>{cycle.hint}</small>
        </div>

        {selectedOffer ? (
          <>
            <div className="plans-v2__price-value">
              {referencePrice && <del>{formatMoney(referencePrice)}</del>}
              <strong>{formatMoney(selectedOffer.priceCents)}</strong>
              <span>{selectedCycle === 1 ? 'por mês' : `por ${selectedCycle} meses`}</span>
            </div>

            <div className="plans-v2__price-meta">
              <span>
                <i className="fa-solid fa-ticket" aria-hidden="true" />
                {signupFee > 0 ? `Adesão ${formatMoney(signupFee)}` : 'Sem taxa de adesão'}
              </span>
              {equivalentMonthly && (
                <span>
                  <i className="fa-solid fa-calculator" aria-hidden="true" />
                  Equivale a {formatMoney(equivalentMonthly)}/mês
                </span>
              )}
            </div>

            <button
              className="plans-v2__select-button"
              type="button"
              onClick={() => onSelectOffer?.(selectedOffer)}
            >
              <span>Escolher este plano</span>
              <i className="fa-solid fa-arrow-right" aria-hidden="true" />
            </button>
          </>
        ) : (
          <div className="plans-v2__unavailable">
            <strong>Modalidade indisponível</strong>
            <span>Este ciclo não está publicado para o plano no catálogo atual.</span>
            <button type="button" disabled>Indisponível</button>
          </div>
        )}
      </div>
    </article>
  );
}

function AnnualPremium({
  offers,
  onSelectOffer,
}: {
  offers: PublicPlanOffer[];
  onSelectOffer?: (offer: PublicPlanOffer) => void;
}) {
  const annualOffers = PLAN_DEFINITIONS
    .map((definition) => {
      const planOffers = getPlanOffers(definition, offers);
      const annual = getOfferByInterval(planOffers, 12);
      const monthly = getOfferByInterval(planOffers, 1);

      return annual
        ? { definition, offer: annual, referencePrice: getReferenceAnnualPrice(annual, monthly) }
        : null;
    })
    .filter((item): item is { definition: PlanDefinition; offer: PublicPlanOffer; referencePrice: number | null } => Boolean(item));

  return (
    <section className="plans-v2__premium" aria-labelledby="plans-premium-title">
      <div className="plans-v2__premium-copy">
        <div className="plans-v2__premium-badge">
          <i className="fa-solid fa-star" aria-hidden="true" />
          MEGA PROMOÇÃO
        </div>
        <span className="plans-v2__premium-kicker">Condição anual em destaque</span>
        <h2 id="plans-premium-title">Ouro Anual</h2>
        <p>
          Para quem prefere contratar o ciclo completo e aproveitar a melhor condição anual publicada no catálogo.
        </p>
        <div className="plans-v2__premium-benefits">
          <span><i className="fa-solid fa-calendar-check" aria-hidden="true" /> 12 meses de acesso</span>
          <span><i className="fa-solid fa-piggy-bank" aria-hidden="true" /> Condição promocional</span>
          <span><i className="fa-solid fa-bolt" aria-hidden="true" /> Menos renovações</span>
        </div>
      </div>

      <div className="plans-v2__premium-offers">
        {annualOffers.length > 0 ? annualOffers.map(({ definition, offer, referencePrice }) => (
          <div className="plans-v2__premium-offer" key={offer.offerId}>
            <div>
              <span>{definition.title}</span>
              <small>{offer.monthlyPublicationLimit} postagem{offer.monthlyPublicationLimit === 1 ? '' : 'ens'}/mês</small>
            </div>
            <div className="plans-v2__premium-price">
              {referencePrice && <del>{formatMoney(referencePrice)}</del>}
              <strong>{formatMoney(offer.priceCents)}</strong>
            </div>
            <button type="button" onClick={() => onSelectOffer?.(offer)} aria-label={`Escolher ${definition.title} anual`}>
              <i className="fa-solid fa-arrow-right" aria-hidden="true" />
            </button>
          </div>
        )) : (
          <div className="plans-v2__premium-empty">
            <i className="fa-solid fa-clock" aria-hidden="true" />
            <span>A condição anual não está publicada no catálogo neste momento.</span>
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
  const [selectedCycle, setSelectedCycle] = useState<BillingCycle>(12);

  const groupedPlans = useMemo(
    () => PLAN_DEFINITIONS.map((definition) => ({ definition, offers: getPlanOffers(definition, offers) })),
    [offers],
  );

  const availableCycles = useMemo(
    () => new Set(offers.map((offer) => offer.billingIntervalMonths)),
    [offers],
  );

  useEffect(() => {
    if (offers.length === 0 || availableCycles.has(selectedCycle)) return;

    const firstAvailable = CYCLE_DEFINITIONS.find((cycle) => availableCycles.has(cycle.months));
    if (firstAvailable) setSelectedCycle(firstAvailable.months);
  }, [availableCycles, offers.length, selectedCycle]);

  return (
    <div className="plans-v2">
      <section className="plans-v2__hero" aria-labelledby="plans-page-title">
        <div className="plans-v2__hero-copy">
          <span className="plans-v2__eyebrow">CIDADEMDIA PARA SUA GESTÃO</span>
          <h1 id="plans-page-title">
            <span>PLANOS PARA</span>
            <span>TODOS OS</span>
            <span>TIPOS DE GESTÃO</span>
          </h1>
          <p>
            Escolha a estrutura ideal para acompanhar ocorrências, gerenciar acessos e ampliar a capacidade de atendimento da sua equipe.
          </p>
          <span className="plans-v2__brand-line" aria-hidden="true" />
        </div>

        <CycleSelector
          selectedCycle={selectedCycle}
          availableCycles={availableCycles}
          onChange={setSelectedCycle}
        />
      </section>

      <section className="plans-v2__benefits" aria-label="Benefícios gerais dos planos">
        {BENEFITS.map((benefit) => (
          <article key={benefit.title}>
            <span className={`plans-v2__benefit-icon plans-v2__benefit-icon--${benefit.tone}`} aria-hidden="true">
              <i className={`fa-solid ${benefit.icon}`} />
            </span>
            <div>
              <h2>{benefit.title}</h2>
              <p>{benefit.description}</p>
            </div>
          </article>
        ))}
      </section>

      <section className="plans-v2__catalog" aria-labelledby="plans-catalog-title">
        <div className="plans-v2__section-heading">
          <span>Estrutura da operação</span>
          <div>
            <h2 id="plans-catalog-title">Escolha o plano. O ciclo já está definido.</h2>
            <p>Os valores e limites abaixo vêm diretamente do catálogo público vigente.</p>
          </div>
        </div>

        {loading && (
          <div className="plans-v2__state" aria-busy="true">
            <i className="fa-solid fa-spinner fa-spin" aria-hidden="true" />
            Carregando condições disponíveis...
          </div>
        )}

        {!loading && unavailable && (
          <div className="plans-v2__state plans-v2__state--error" role="alert">
            <i className="fa-solid fa-circle-exclamation" aria-hidden="true" />
            Não foi possível consultar o catálogo agora. Tente novamente em instantes.
          </div>
        )}

        {!loading && !unavailable && offers.length === 0 && (
          <div className="plans-v2__state">
            <i className="fa-solid fa-layer-group" aria-hidden="true" />
            Nenhuma oferta está publicada no catálogo neste momento.
          </div>
        )}

        {!loading && (
          <div className="plans-v2__plan-grid">
            {groupedPlans.map(({ definition, offers: planOffers }) => (
              <PlanCard
                definition={definition}
                key={definition.id}
                offers={planOffers}
                selectedCycle={selectedCycle}
                onSelectOffer={onSelectOffer}
              />
            ))}
          </div>
        )}
      </section>

      {!loading && <AnnualPremium offers={offers} onSelectOffer={onSelectOffer} />}

      <section className="plans-v2__trust" aria-label="Confiança e suporte">
        <article className="plans-v2__trust-contact">
          <span className="plans-v2__trust-icon" aria-hidden="true">
            <i className="fa-solid fa-comments" />
          </span>
          <div>
            <strong>Precisa de outra composição?</strong>
            <span>Fale com a equipe para avaliar um plano personalizado.</span>
          </div>
          <button type="button" onClick={onContact}>Fale conosco</button>
        </article>

        {TRUST_ITEMS.map((item) => (
          <article key={item.title}>
            <span className="plans-v2__trust-icon" aria-hidden="true">
              <i className={`fa-solid ${item.icon}`} />
            </span>
            <div>
              <strong>{item.title}</strong>
              <span>{item.text}</span>
            </div>
          </article>
        ))}
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
      loading={loading}
      offers={offers}
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

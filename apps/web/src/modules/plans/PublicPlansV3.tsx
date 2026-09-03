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

type BillingCycle = 1 | 3 | 6 | 12;
type PlanTone = 'blue' | 'green' | 'sky';

interface PlanDefinition {
  id: 'individual' | 'master-5' | 'master-10';
  title: string;
  icon: string;
  tone: PlanTone;
  subaccountLimit: number;
  descriptor: string;
}

const PLAN_DEFINITIONS: PlanDefinition[] = [
  { id: 'individual', title: 'Individual', icon: 'fa-user', tone: 'blue', subaccountLimit: 0, descriptor: 'Para uma gestão direta, simples e objetiva.' },
  { id: 'master-5', title: 'Master 5', icon: 'fa-building-user', tone: 'green', subaccountLimit: 5, descriptor: 'Para equipes que precisam distribuir acessos.' },
  { id: 'master-10', title: 'Master 10', icon: 'fa-people-group', tone: 'sky', subaccountLimit: 10, descriptor: 'Para operações com mais pessoas e volume.' },
];

const CYCLES: Array<{ months: BillingCycle; label: string; suffix: string }> = [
  { months: 1, label: 'Mensal', suffix: 'por mês' },
  { months: 3, label: 'Trimestral', suffix: 'por 3 meses' },
  { months: 6, label: 'Semestral', suffix: 'por 6 meses' },
  { months: 12, label: 'Anual', suffix: 'por 12 meses' },
];

const BENEFITS = [
  { icon: 'fa-clipboard-list', tone: 'blue', title: 'Acesso às ocorrências', description: 'Acompanhe as demandas compartilhadas com sua gestão.' },
  { icon: 'fa-users-gear', tone: 'green', title: 'Gerencie subcontas', description: 'Distribua acessos conforme a estrutura contratada.' },
  { icon: 'fa-bell', tone: 'sky', title: 'Receba notificações', description: 'Não perca movimentações importantes da operação.' },
  { icon: 'fa-photo-film', tone: 'yellow', title: 'Postagens mensais', description: 'Publique de acordo com a franquia vigente do plano.' },
] as const;

const TRUST_ITEMS = [
  { icon: 'fa-shield-halved', title: 'Pagamento seguro', text: 'Fluxos protegidos e dados tratados com responsabilidade.' },
  { icon: 'fa-headset', title: 'Suporte dedicado', text: 'Apoio para orientar o uso dos recursos contratados.' },
  { icon: 'fa-ticket', title: 'Adesão transparente', text: 'A taxa aplicável aparece junto da condição escolhida.' },
  { icon: 'fa-microchip', title: 'Tecnologia conectada', text: 'Ocorrências, equipes e publicações no mesmo ambiente.' },
] as const;

function normalize(value: string) {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim();
}

function formatMoney(valueInCents: number) {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valueInCents / 100);
}

function offerMatchesPlan(definition: PlanDefinition, offer: PublicPlanOffer) {
  const searchable = normalize(`${offer.planKey} ${offer.planName}`);
  if (definition.id === 'individual') return searchable.includes('individual') || offer.subaccountLimit === 0;
  if (definition.id === 'master-5') return searchable.includes('master 5') || offer.subaccountLimit === 5;
  return searchable.includes('master 10') || offer.subaccountLimit === 10;
}

function getPlanOffers(definition: PlanDefinition, offers: PublicPlanOffer[]) {
  return offers.filter((offer) => offerMatchesPlan(definition, offer));
}

function getOfferByInterval(offers: PublicPlanOffer[], months: BillingCycle) {
  return offers.find((offer) => offer.billingIntervalMonths === months);
}

function getReferenceAnnualPrice(annual: PublicPlanOffer, monthly?: PublicPlanOffer) {
  if (annual.marketingReferencePriceCents && annual.marketingReferencePriceCents > annual.priceCents) return annual.marketingReferencePriceCents;
  if (monthly && monthly.priceCents * 12 > annual.priceCents) return monthly.priceCents * 12;
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
          <button type="button" onClick={onHome}><i className="fa-solid fa-house" aria-hidden="true" />Início</button>
          <a className="plans-page__nav-active" href="/planos" aria-current="page"><i className="fa-solid fa-layer-group" aria-hidden="true" />Planos</a>
        </nav>
        <div className="plans-page__header-actions">
          <Button variant="ghost" onClick={onLogin}>Entrar</Button>
          <Button onClick={onRegister}>Criar conta</Button>
        </div>
      </div>
    </header>
  );
}

function CycleSelector({ selected, available, onChange }: { selected: BillingCycle; available: Set<number>; onChange: (cycle: BillingCycle) => void }) {
  return (
    <div className="plans-v3__cycle-wrap">
      <span className="plans-v3__cycle-label">Periodicidade</span>
      <div className="plans-v3__cycle-selector" role="group" aria-label="Periodicidade do plano">
        {CYCLES.map((cycle) => {
          const enabled = available.size === 0 || available.has(cycle.months);
          return (
            <button
              key={cycle.months}
              type="button"
              className={selected === cycle.months ? 'plans-v3__cycle-button plans-v3__cycle-button--active' : 'plans-v3__cycle-button'}
              aria-pressed={selected === cycle.months}
              disabled={!enabled}
              onClick={() => onChange(cycle.months)}
            >
              {cycle.label}
              {cycle.months === 12 && <small>melhor condição</small>}
            </button>
          );
        })}
      </div>
    </div>
  );
}

function RegularPlanCard({ definition, planOffers, selectedCycle, onSelectOffer }: { definition: PlanDefinition; planOffers: PublicPlanOffer[]; selectedCycle: BillingCycle; onSelectOffer?: (offer: PublicPlanOffer) => void }) {
  const offer = getOfferByInterval(planOffers, selectedCycle);
  const monthly = getOfferByInterval(planOffers, 1);
  const cycle = CYCLES.find((item) => item.months === selectedCycle) ?? CYCLES[0];
  const reference = offer && selectedCycle === 12 ? getReferenceAnnualPrice(offer, monthly) : null;
  const signupFee = offer?.signupFeeCents ?? 0;
  const subaccounts = offer?.subaccountLimit ?? planOffers[0]?.subaccountLimit ?? definition.subaccountLimit;
  const publicationLimit = offer?.monthlyPublicationLimit ?? Math.max(0, ...planOffers.map((item) => item.monthlyPublicationLimit));

  return (
    <article className={`plans-v3__plan plans-v3__plan--${definition.tone}`}>
      <div className="plans-v3__plan-head">
        <span className="plans-v3__plan-icon" aria-hidden="true"><i className={`fa-solid ${definition.icon}`} /></span>
        <div><small>Plano</small><h2>{definition.title}</h2></div>
        <span className="plans-v3__posting-badge">{publicationLimit > 0 ? `${publicationLimit} postagens/mês` : 'Conforme catálogo'}</span>
      </div>
      <p className="plans-v3__descriptor">{definition.descriptor}</p>
      <div className="plans-v3__facts">
        <span><i className="fa-solid fa-chart-line" aria-hidden="true" />1 Painel Master</span>
        <span><i className="fa-solid fa-user-group" aria-hidden="true" />{subaccounts} subconta{subaccounts === 1 ? '' : 's'}</span>
      </div>
      <div className="plans-v3__price-block">
        <span className="plans-v3__price-cycle">{cycle.label}</span>
        {offer ? (
          <>
            <div className="plans-v3__price-row">
              <div>{reference && <del>{formatMoney(reference)}</del>}<strong>{formatMoney(offer.priceCents)}</strong><small>{cycle.suffix}</small></div>
              <span className="plans-v3__signup">{signupFee > 0 ? `Adesão ${formatMoney(signupFee)}` : 'Sem taxa de adesão'}</span>
            </div>
            <button className="plans-v3__choose" type="button" onClick={() => onSelectOffer?.(offer)}>
              Escolher plano <i className="fa-solid fa-arrow-right" aria-hidden="true" />
            </button>
          </>
        ) : (
          <div className="plans-v3__unavailable"><strong>Indisponível neste ciclo</strong><span>Esta modalidade não está publicada no catálogo atual.</span></div>
        )}
      </div>
    </article>
  );
}

function PremiumAnnualCard({ offers, onSelectOffer }: { offers: PublicPlanOffer[]; onSelectOffer?: (offer: PublicPlanOffer) => void }) {
  const annualOffers = PLAN_DEFINITIONS.map((definition) => {
    const planOffers = getPlanOffers(definition, offers);
    const annual = getOfferByInterval(planOffers, 12);
    const monthly = getOfferByInterval(planOffers, 1);
    return annual ? { definition, offer: annual, reference: getReferenceAnnualPrice(annual, monthly) } : null;
  }).filter((item): item is { definition: PlanDefinition; offer: PublicPlanOffer; reference: number | null } => Boolean(item));

  const cheapest = annualOffers.reduce<(typeof annualOffers)[number] | null>((current, item) => !current || item.offer.priceCents < current.offer.priceCents ? item : current, null);

  return (
    <aside className="plans-v3__premium" aria-label="Mega Promoção anual">
      <div className="plans-v3__premium-stars" aria-hidden="true"><span>✦</span><span>✧</span><span>✦</span></div>
      <span className="plans-v3__premium-badge"><i className="fa-solid fa-star" aria-hidden="true" />MEGA PROMOÇÃO</span>
      <small className="plans-v3__premium-kicker">Plano Ouro Anual</small>
      <h2>12 meses com condição especial.</h2>
      <p>O anual fica sempre disponível aqui e não muda quando você troca a periodicidade dos planos ao lado.</p>

      {cheapest && (
        <div className="plans-v3__premium-main-price">
          <span>A partir de</span>
          {cheapest.reference && <del>{formatMoney(cheapest.reference)}</del>}
          <strong>{formatMoney(cheapest.offer.priceCents)}</strong>
          <small>{cheapest.definition.title} · anual</small>
        </div>
      )}

      <div className="plans-v3__premium-list">
        {annualOffers.length > 0 ? annualOffers.map(({ definition, offer, reference }) => (
          <button key={offer.offerId} type="button" onClick={() => onSelectOffer?.(offer)} aria-label={`Escolher ${definition.title} anual`}>
            <span><strong>{definition.title}</strong><small>{offer.monthlyPublicationLimit} postagens/mês</small></span>
            <span>{reference && <del>{formatMoney(reference)}</del>}<strong>{formatMoney(offer.priceCents)}</strong></span>
            <i className="fa-solid fa-arrow-right" aria-hidden="true" />
          </button>
        )) : <div className="plans-v3__premium-empty">Nenhuma oferta anual publicada no momento.</div>}
      </div>

      <div className="plans-v3__premium-foot"><span><i className="fa-solid fa-calendar-check" aria-hidden="true" />12 meses</span><span><i className="fa-solid fa-piggy-bank" aria-hidden="true" />Mais economia</span></div>
    </aside>
  );
}

function BenefitsFooter({ onContact }: { onContact?: () => void }) {
  return (
    <footer className="plans-v3__info-footer">
      <div className="plans-v3__benefits" aria-label="Benefícios gerais dos planos">
        {BENEFITS.map((benefit) => (
          <article key={benefit.title}>
            <span className={`plans-v3__benefit-icon plans-v3__benefit-icon--${benefit.tone}`} aria-hidden="true"><i className={`fa-solid ${benefit.icon}`} /></span>
            <div><strong>{benefit.title}</strong><span>{benefit.description}</span></div>
          </article>
        ))}
      </div>
      <div className="plans-v3__trust">
        <article className="plans-v3__trust-contact">
          <span className="plans-v3__trust-icon" aria-hidden="true"><i className="fa-solid fa-comments" /></span>
          <div><strong>Precisa de outra composição?</strong><span>Fale com a equipe para avaliar um plano personalizado.</span></div>
          <button type="button" onClick={onContact}>Fale conosco</button>
        </article>
        {TRUST_ITEMS.map((item) => (
          <article key={item.title}>
            <span className="plans-v3__trust-icon" aria-hidden="true"><i className={`fa-solid ${item.icon}`} /></span>
            <div><strong>{item.title}</strong><span>{item.text}</span></div>
          </article>
        ))}
      </div>
    </footer>
  );
}

function PlansContent({ offers, loading, unavailable, onSelectOffer }: { offers: PublicPlanOffer[]; loading: boolean; unavailable: boolean; onSelectOffer?: (offer: PublicPlanOffer) => void }) {
  const [selectedCycle, setSelectedCycle] = useState<BillingCycle>(1);
  const groupedPlans = useMemo(() => PLAN_DEFINITIONS.map((definition) => ({ definition, offers: getPlanOffers(definition, offers) })), [offers]);
  const availableCycles = useMemo(() => new Set(offers.map((offer) => offer.billingIntervalMonths)), [offers]);

  useEffect(() => {
    if (offers.length === 0 || availableCycles.has(selectedCycle)) return;
    const fallback = CYCLES.find((cycle) => availableCycles.has(cycle.months));
    if (fallback) setSelectedCycle(fallback.months);
  }, [availableCycles, offers.length, selectedCycle]);

  return (
    <main className="plans-v3">
      <section className="plans-v3__pricing-layout" aria-label="Planos CIDADEMDIA">
        <div className="plans-v3__regular-area">
          <CycleSelector selected={selectedCycle} available={availableCycles} onChange={setSelectedCycle} />

          {loading && <div className="plans-v3__state" aria-busy="true"><i className="fa-solid fa-spinner fa-spin" aria-hidden="true" />Carregando condições disponíveis...</div>}
          {!loading && unavailable && <div className="plans-v3__state plans-v3__state--error" role="alert"><i className="fa-solid fa-circle-exclamation" aria-hidden="true" />Não foi possível consultar o catálogo agora.</div>}
          {!loading && !unavailable && offers.length === 0 && <div className="plans-v3__state"><i className="fa-solid fa-layer-group" aria-hidden="true" />Nenhuma oferta está publicada no catálogo neste momento.</div>}

          {!loading && (
            <div className="plans-v3__regular-grid">
              {groupedPlans.map(({ definition, offers: planOffers }) => (
                <RegularPlanCard key={definition.id} definition={definition} planOffers={planOffers} selectedCycle={selectedCycle} onSelectOffer={onSelectOffer} />
              ))}
            </div>
          )}
        </div>

        {!loading && <PremiumAnnualCard offers={offers} onSelectOffer={onSelectOffer} />}
      </section>
    </main>
  );
}

export function PublicPlansV3({ onHome, onLogin, onRegister, embedded = false, onSelectOffer, onContact }: PublicPlansProps) {
  const [offers, setOffers] = useState<PublicPlanOffer[]>([]);
  const [loading, setLoading] = useState(true);
  const [unavailable, setUnavailable] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setUnavailable(false);
    void listPublicPlanOffers()
      .then((items) => { if (active) setOffers(items); })
      .catch(() => { if (active) { setOffers([]); setUnavailable(true); } })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  const body = <PlansContent offers={offers} loading={loading} unavailable={unavailable} onSelectOffer={onSelectOffer} />;
  const infoFooter = <BenefitsFooter onContact={onContact} />;

  if (embedded) return <div className="plans-page plans-page--embedded">{body}{infoFooter}</div>;

  return (
    <div className="plans-page">
      <PublicPlansHeader onHome={onHome} onLogin={onLogin} onRegister={onRegister} />
      {body}
      {infoFooter}
      <footer className="plans-page__footer"><div className="plans-page__footer-inner"><Brand compact /><span>CIDADEMDIA — conectando cidadãos e quem pode resolver.</span></div></footer>
    </div>
  );
}

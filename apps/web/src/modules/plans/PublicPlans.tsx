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

interface PlanDefinition {
  id: 'individual' | 'master-5' | 'master-10';
  title: string;
  icon: string;
  tone: PlanTone;
  subaccountLimit: number;
}

interface PaymentDefinition {
  months: number;
  title: string;
  tone: 'basic' | 'bronze' | 'silver' | 'gold';
}

const PLAN_DEFINITIONS: PlanDefinition[] = [
  { id: 'individual', title: 'Individual', icon: 'fa-user', tone: 'blue', subaccountLimit: 0 },
  { id: 'master-5', title: 'Master 5', icon: 'fa-building-user', tone: 'green', subaccountLimit: 5 },
  { id: 'master-10', title: 'Master 10', icon: 'fa-people-group', tone: 'sky', subaccountLimit: 10 },
];

const PAYMENT_DEFINITIONS: PaymentDefinition[] = [
  { months: 1, title: 'Básico Mensal', tone: 'basic' },
  { months: 3, title: 'Bronze Trimestral', tone: 'bronze' },
  { months: 6, title: 'Prata Semestral', tone: 'silver' },
  { months: 12, title: 'Ouro Anual', tone: 'gold' },
];

const BENEFITS = [
  {
    icon: 'fa-clipboard-list',
    title: 'Acesso às ocorrências',
    description: 'Visualize e acompanhe as demandas compartilhadas com sua gestão.',
    tone: 'blue',
  },
  {
    icon: 'fa-users-gear',
    title: 'Gerencie subcontas',
    description: 'Organize equipes e distribua acessos conforme a capacidade do plano.',
    tone: 'green',
  },
  {
    icon: 'fa-bell',
    title: 'Receba notificações',
    description: 'Acompanhe movimentações importantes sem perder atualizações.',
    tone: 'orange',
  },
  {
    icon: 'fa-photo-film',
    title: 'Postagens mensais',
    description: 'Publique conteúdos institucionais de acordo com a franquia contratada.',
    tone: 'yellow',
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

function getOfferByInterval(offers: PublicPlanOffer[], months: number) {
  return offers.find((offer) => offer.billingIntervalMonths === months);
}

function getSignupFee(offers: PublicPlanOffer[]) {
  const catalogFee = offers.reduce((highest, offer) => Math.max(highest, offer.signupFeeCents), 0);
  return catalogFee || 30000;
}

function getPublicationLimit(offers: PublicPlanOffer[]) {
  return offers.reduce((highest, offer) => Math.max(highest, offer.monthlyPublicationLimit), 0);
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

function PaymentOption({
  definition,
  offer,
  monthlyOffer,
  onSelectOffer,
}: {
  definition: PaymentDefinition;
  offer?: PublicPlanOffer;
  monthlyOffer?: PublicPlanOffer;
  onSelectOffer?: (offer: PublicPlanOffer) => void;
}) {
  const isAnnual = definition.months === 12;
  const referencePrice = offer && isAnnual ? getReferenceAnnualPrice(offer, monthlyOffer) : null;

  return (
    <div className={`plans-page__payment plans-page__payment--${definition.tone}`}>
      <div className="plans-page__payment-heading">
        <span>{definition.title}</span>
        {isAnnual && <small>Promocional</small>}
      </div>

      {offer ? (
        <>
          <div className="plans-page__payment-price">
            {referencePrice && <del>{formatMoney(referencePrice)}</del>}
            <strong>{formatMoney(offer.priceCents)}</strong>
            <span>
              {definition.months === 1
                ? 'por mês'
                : `a cada ${definition.months} meses`}
            </span>
          </div>

          {isAnnual && (
            <p className="plans-page__installment">
              Equivalente a 12x de {formatMoney(Math.round(offer.priceCents / 12))}.
            </p>
          )}

          <button
            className="plans-page__choose-button"
            type="button"
            onClick={() => onSelectOffer?.(offer)}
          >
            Escolher esse plano
          </button>
        </>
      ) : (
        <div className="plans-page__payment-unavailable">
          <strong>Indisponível</strong>
          <span>Esta modalidade não está publicada no catálogo atual.</span>
          <button className="plans-page__choose-button" type="button" disabled>
            Escolher esse plano
          </button>
        </div>
      )}
    </div>
  );
}

function PlanCard({
  definition,
  offers,
  onSelectOffer,
}: {
  definition: PlanDefinition;
  offers: PublicPlanOffer[];
  onSelectOffer?: (offer: PublicPlanOffer) => void;
}) {
  const monthlyOffer = getOfferByInterval(offers, 1);
  const publicationLimit = getPublicationLimit(offers);
  const subaccountLimit = offers[0]?.subaccountLimit ?? definition.subaccountLimit;
  const signupFee = getSignupFee(offers);

  return (
    <article className={`plans-page__plan-card plans-page__plan-card--${definition.tone}`}>
      <div className="plans-page__posting-badge">
        <i className="fa-solid fa-bullhorn" aria-hidden="true" />
        {publicationLimit > 0
          ? `${publicationLimit} postagem${publicationLimit === 1 ? '' : 'ens'}/mês`
          : 'Postagens conforme catálogo'}
      </div>

      <div className="plans-page__plan-title">
        <span className="plans-page__plan-icon" aria-hidden="true">
          <i className={`fa-solid ${definition.icon}`} />
        </span>
        <div>
          <small>Plano</small>
          <h2>{definition.title}</h2>
        </div>
      </div>

      <div className="plans-page__signup-fee">
        <i className="fa-solid fa-ticket" aria-hidden="true" />
        Adesão única {formatMoney(signupFee)}
      </div>

      <div className="plans-page__plan-summary">
        <span><i className="fa-solid fa-chart-line" aria-hidden="true" /> 1 Painel Master</span>
        <span>
          <i className="fa-solid fa-user-group" aria-hidden="true" />
          {subaccountLimit} subconta{subaccountLimit === 1 ? '' : 's'} incluída{subaccountLimit === 1 ? '' : 's'}
        </span>
      </div>

      <div className="plans-page__payments">
        {PAYMENT_DEFINITIONS.map((payment) => (
          <PaymentOption
            definition={payment}
            key={payment.months}
            monthlyOffer={monthlyOffer}
            offer={getOfferByInterval(offers, payment.months)}
            onSelectOffer={onSelectOffer}
          />
        ))}
      </div>
    </article>
  );
}

function MegaPromotion({ offers }: { offers: PublicPlanOffer[] }) {
  const annualOffers = PLAN_DEFINITIONS
    .map((definition) => ({
      definition,
      offer: getOfferByInterval(getPlanOffers(definition, offers), 12),
    }))
    .filter((item): item is { definition: PlanDefinition; offer: PublicPlanOffer } => Boolean(item.offer));

  return (
    <article className="plans-page__mega-card">
      <span className="plans-page__mega-badge">MEGA PROMOÇÃO</span>
      <i className="fa-solid fa-star plans-page__mega-star" aria-hidden="true" />
      <span className="plans-page__mega-eyebrow">Condição especial</span>
      <h2>PLANO OURO ANUAL</h2>
      <p>Uma condição criada para quem quer ampliar a gestão com previsibilidade durante todo o ano.</p>

      <div className="plans-page__mega-condition">
        <span>Adesão</span>
        <strong>ISENTA</strong>
        <small>na condição promocional anual</small>
      </div>

      <div className="plans-page__mega-condition plans-page__mega-condition--yellow">
        <span>Ciclo anual</span>
        <strong>11 MENSALIDADES</strong>
        <small>12 meses de acesso na condição promocional</small>
      </div>

      {annualOffers.length > 0 && (
        <div className="plans-page__mega-prices">
          <span>Ofertas anuais publicadas</span>
          {annualOffers.map(({ definition, offer }) => (
            <div key={offer.offerId}>
              <small>{definition.title}</small>
              <strong>{formatMoney(offer.priceCents)}</strong>
            </div>
          ))}
        </div>
      )}

      <div className="plans-page__mega-benefits">
        <div><i className="fa-solid fa-gauge-high" aria-hidden="true" /><span>Mais eficiência</span></div>
        <div><i className="fa-solid fa-piggy-bank" aria-hidden="true" /><span>Mais economia</span></div>
        <div><i className="fa-solid fa-microchip" aria-hidden="true" /><span>Mais tecnologia</span></div>
      </div>
    </article>
  );
}

function PlansContent({ offers, loading, unavailable, onSelectOffer, onContact }: {
  offers: PublicPlanOffer[];
  loading: boolean;
  unavailable: boolean;
  onSelectOffer?: (offer: PublicPlanOffer) => void;
  onContact?: () => void;
}) {
  const groupedPlans = useMemo(
    () => PLAN_DEFINITIONS.map((definition) => ({ definition, offers: getPlanOffers(definition, offers) })),
    [offers],
  );

  return (
    <div className="plans-page__content">
      <section className="plans-page__intro" aria-labelledby="plans-page-title">
        <span className="plans-page__intro-kicker">CIDADEMDIA PARA SUA GESTÃO</span>
        <h1 id="plans-page-title">
          <span className="plans-page__headline-blue">PLANOS PARA</span>
          <span className="plans-page__headline-green">TODOS OS</span>
          <span className="plans-page__headline-yellow">TIPOS DE GESTÃO</span>
        </h1>
        <p>
          Escolha a estrutura ideal para acompanhar ocorrências, gerenciar acessos e ampliar a capacidade de atendimento da sua equipe.
        </p>
        <span className="plans-page__intro-line" aria-hidden="true" />
      </section>

      <section className="plans-page__benefits" aria-label="Benefícios gerais dos planos">
        {BENEFITS.map((benefit) => (
          <article key={benefit.title}>
            <span className={`plans-page__benefit-icon plans-page__benefit-icon--${benefit.tone}`} aria-hidden="true">
              <i className={`fa-solid ${benefit.icon}`} />
            </span>
            <div>
              <h2>{benefit.title}</h2>
              <p>{benefit.description}</p>
            </div>
          </article>
        ))}
      </section>

      <section className="plans-page__catalog" aria-labelledby="plans-catalog-title">
        <div className="plans-page__catalog-heading">
          <span>Escolha sua estrutura</span>
          <h2 id="plans-catalog-title">Planos e condições de pagamento</h2>
          <p>Os valores abaixo são carregados diretamente do catálogo público vigente do CIDADEMDIA.</p>
        </div>

        {loading && (
          <div className="plans-page__catalog-state" aria-busy="true">
            <i className="fa-solid fa-spinner fa-spin" aria-hidden="true" />
            Carregando condições disponíveis...
          </div>
        )}

        {!loading && unavailable && (
          <div className="plans-page__catalog-state plans-page__catalog-state--error" role="alert">
            <i className="fa-solid fa-circle-exclamation" aria-hidden="true" />
            Não foi possível consultar o catálogo agora. Tente novamente em instantes.
          </div>
        )}

        {!loading && !unavailable && offers.length === 0 && (
          <div className="plans-page__catalog-state">
            <i className="fa-solid fa-layer-group" aria-hidden="true" />
            Nenhuma oferta está publicada no catálogo neste momento.
          </div>
        )}

        {!loading && (
          <div className="plans-page__plans-grid">
            {groupedPlans.map(({ definition, offers: planOffers }) => (
              <PlanCard
                definition={definition}
                key={definition.id}
                offers={planOffers}
                onSelectOffer={onSelectOffer}
              />
            ))}
            <MegaPromotion offers={offers} />
          </div>
        )}
      </section>

      <section className="plans-page__complementary" aria-label="Informações complementares dos planos">
        <article className="plans-page__complementary-contact">
          <i className="fa-solid fa-comments" aria-hidden="true" />
          <div>
            <strong>Precisa de um plano personalizado?</strong>
            <span>Converse com a equipe para avaliar uma composição específica para sua operação.</span>
          </div>
          <button type="button" onClick={onContact}>Fale conosco</button>
        </article>

        <article>
          <i className="fa-solid fa-ticket" aria-hidden="true" />
          <div><strong>Adesão única</strong><span>Cobrada uma única vez nos planos aplicáveis, conforme a oferta contratada.</span></div>
        </article>
        <article>
          <i className="fa-solid fa-shield-halved" aria-hidden="true" />
          <div><strong>Pagamento e dados seguros</strong><span>Fluxos protegidos e tratamento responsável das informações da plataforma.</span></div>
        </article>
        <article>
          <i className="fa-solid fa-headset" aria-hidden="true" />
          <div><strong>Suporte dedicado</strong><span>Apoio para orientar o uso dos recursos disponíveis no plano contratado.</span></div>
        </article>
        <article>
          <i className="fa-solid fa-city" aria-hidden="true" />
          <div><strong>Uma plataforma conectada</strong><span>Ocorrências, equipes, publicações e acompanhamento reunidos em um só ambiente.</span></div>
        </article>
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

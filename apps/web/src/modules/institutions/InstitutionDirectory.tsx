import { useEffect, useMemo, useState } from 'react';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import { listInstitutions } from './institutionService';
import type { InstitutionItem, InstitutionRepresentativeItem } from './types';

const institutionTypeLabels: Record<string, string> = {
  CITY_HALL: 'Prefeitura',
  CITY_COUNCIL: 'Câmara Municipal',
  ASSEMBLY: 'Assembleia',
  PUBLIC_AGENCY: 'Órgão público',
  PUBLIC_SERVICE: 'Serviço público',
  OTHER: 'Instituição',
};

const scopeLabels: Record<string, string> = {
  MUNICIPAL: 'Municipal',
  STATE: 'Estadual',
  FEDERAL: 'Federal',
  REGIONAL: 'Regional',
  OTHER: 'Outro',
};

function representativeStatusLabel(representative: InstitutionRepresentativeItem) {
  switch (representative.profileStatus) {
    case 'ACTIVE':
      return 'Perfil ativo';
    case 'INVITED':
      return 'Convite enviado';
    case 'INACTIVE':
      return 'Inativo';
    default:
      return 'Ainda não aderiu';
  }
}

function representativeStatusVariant(representative: InstitutionRepresentativeItem) {
  return representative.profileStatus === 'ACTIVE'
    ? 'success' as const
    : representative.profileStatus === 'INVITED'
      ? 'info' as const
      : 'neutral' as const;
}

function InstitutionCard({ institution }: { institution: InstitutionItem }) {
  return (
    <Card className="institution-directory__card">
      <CardBody>
        <div className="institution-directory__card-header">
          <div>
            <Badge variant="primary">
              {institutionTypeLabels[institution.type] ?? institution.type}
            </Badge>
            <h3>{institution.name}</h3>
          </div>
          <span className="institution-directory__scope">
            {scopeLabels[institution.scopeLevel] ?? institution.scopeLevel}
            {institution.stateCode ? ` · ${institution.stateCode}` : ''}
          </span>
        </div>

        {institution.description ? (
          <p className="institution-directory__description">{institution.description}</p>
        ) : null}

        {institution.representatives.length === 0 ? (
          <p className="institution-directory__empty">Nenhum representante cadastrado ainda.</p>
        ) : (
          <div className="institution-directory__representatives">
            <span className="institution-directory__label">Representantes</span>
            {institution.representatives.map((representative) => (
              <div className="institution-directory__representative" key={representative.id}>
                <div>
                  <strong>{representative.name}</strong>
                  <span>{representative.publicRole}</span>
                </div>
                <Badge variant={representativeStatusVariant(representative)}>
                  {representativeStatusLabel(representative)}
                </Badge>
              </div>
            ))}
          </div>
        )}
      </CardBody>
    </Card>
  );
}

export function InstitutionDirectory() {
  const [institutions, setInstitutions] = useState<InstitutionItem[]>([]);
  const [search, setSearch] = useState('');
  const [appliedSearch, setAppliedSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(6);
  const [totalItems, setTotalItems] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);

    void listInstitutions({
      search: appliedSearch || undefined,
      page,
      pageSize,
    })
      .then((result) => {
        if (!active) return;
        setInstitutions(result.items);
        setTotalItems(result.totalItems);
      })
      .catch(() => {
        if (active) setError('Não foi possível carregar o diretório institucional agora.');
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [appliedSearch, page, pageSize]);

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil(totalItems / pageSize)),
    [pageSize, totalItems],
  );

  function applySearch() {
    setPage(1);
    setAppliedSearch(search.trim());
  }

  function clearSearch() {
    setSearch('');
    setAppliedSearch('');
    setPage(1);
  }

  return (
    <section
      className="dashboard-section institution-directory"
      id="dashboard-institutions"
      aria-labelledby="institution-directory-title"
    >
      <SectionHeading
        title="Instituições e representantes"
        subtitle="Consulte órgãos e representantes cadastrados no CidadeEmDia, inclusive perfis que ainda não aderiram à plataforma."
      />

      <div className="institution-directory__filters">
        <label>
          <span>Buscar instituição ou representante</span>
          <input
            type="search"
            value={search}
            placeholder="Ex.: Prefeitura, Câmara, nome do representante"
            onChange={(event) => setSearch(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                event.preventDefault();
                applySearch();
              }
            }}
          />
        </label>
        <div className="institution-directory__filter-actions">
          <Button onClick={applySearch}>Buscar</Button>
          {(search || appliedSearch) ? (
            <Button variant="secondary" onClick={clearSearch}>Limpar</Button>
          ) : null}
        </div>
      </div>

      {loading ? (
        <Card><CardBody><p>Carregando diretório...</p></CardBody></Card>
      ) : error ? (
        <Card><CardBody><p className="institution-directory__error" role="alert">{error}</p></CardBody></Card>
      ) : institutions.length === 0 ? (
        <Card>
          <CardBody>
            <p>Nenhuma instituição encontrada{appliedSearch ? ' para essa busca' : ''}.</p>
          </CardBody>
        </Card>
      ) : (
        <>
          <div className="institution-directory__grid">
            {institutions.map((institution) => (
              <InstitutionCard key={institution.id} institution={institution} />
            ))}
          </div>

          {totalPages > 1 ? (
            <div className="institution-directory__pagination" aria-label="Paginação do diretório">
              <Button
                variant="secondary"
                disabled={page <= 1}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
              >
                Anterior
              </Button>
              <span>Página {page} de {totalPages}</span>
              <Button
                variant="secondary"
                disabled={page >= totalPages}
                onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
              >
                Próxima
              </Button>
            </div>
          ) : null}
        </>
      )}
    </section>
  );
}

import { useEffect, useMemo, useState } from 'react';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import { listActiveMasters } from './institutionService';
import type { MasterDirectoryInstitutionItem, MasterDirectoryItem } from './types';

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

function institutionMeta(institution: MasterDirectoryInstitutionItem) {
  return [
    institutionTypeLabels[institution.type] ?? institution.type,
    scopeLabels[institution.scopeLevel] ?? institution.scopeLevel,
    institution.stateCode,
  ].filter(Boolean).join(' · ');
}

function MasterCard({ master }: { master: MasterDirectoryItem }) {
  return (
    <Card className="institution-directory__card">
      <CardBody>
        <div className="institution-directory__card-header">
          <div>
            <Badge variant="success">Master ativo</Badge>
            <h3>{master.displayName}</h3>
          </div>
          <span className="institution-directory__scope">
            {master.institutions.length === 0
              ? 'Sem órgão vinculado'
              : `${master.institutions.length} ${master.institutions.length === 1 ? 'vínculo' : 'vínculos'}`}
          </span>
        </div>

        {master.institutions.length === 0 ? (
          <p className="institution-directory__empty">
            Conta Master ativa sem órgão público vinculado.
          </p>
        ) : (
          <div className="institution-directory__representatives">
            <span className="institution-directory__label">Agentes públicos</span>
            {master.institutions.map((institution) => (
              <div className="institution-directory__representative" key={institution.institutionId}>
                <div>
                  <strong>{institution.name}</strong>
                  <span>
                    {institution.publicRole
                      ? `${institution.publicRole} · ${institutionMeta(institution)}`
                      : institutionMeta(institution)}
                  </span>
                </div>
                <Badge variant="primary">
                  {institutionTypeLabels[institution.type] ?? 'Instituição'}
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
  const [masters, setMasters] = useState<MasterDirectoryItem[]>([]);
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

    void listActiveMasters({
      search: appliedSearch || undefined,
      page,
      pageSize,
    })
      .then((result) => {
        if (!active) return;
        setMasters(result.items);
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
        title="Órgãos e agentes públicos"
        subtitle="Consulte órgãos e agentes públicos cadastrados no CidadeEmDia."
      />

      <div className="institution-directory__filters">
        <label>
          <span>Buscar órgão ou agente público</span>
          <input
            type="search"
            value={search}
            placeholder="Ex.: Prefeitura, Câmara, nome do agente público"
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
      ) : masters.length === 0 ? (
        <Card>
          <CardBody>
            <p>Nenhuma conta Master ativa encontrada{appliedSearch ? ' para essa busca' : ''}.</p>
          </CardBody>
        </Card>
      ) : (
        <>
          <div className="institution-directory__grid">
            {masters.map((master) => (
              <MasterCard key={master.id} master={master} />
            ))}
          </div>

          {totalPages > 1 ? (
            <div className="institution-directory__pagination" aria-label="Paginação das contas Master">
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

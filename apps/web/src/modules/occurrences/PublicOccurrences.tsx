import { useCallback, useEffect, useState } from 'react';
import { Button } from '../../components/ui';
import {
  searchPublicOccurrences,
  type PublicOccurrencePage,
} from '../home/homeService';
import { PublicOccurrenceCard } from './PublicOccurrenceCard';
import { PublicOccurrenceMapFilter } from './PublicOccurrenceMapFilter';
import {
  DEFAULT_PUBLIC_OCCURRENCE_CITY,
  DEFAULT_PUBLIC_OCCURRENCE_RADIUS_KM,
  geocodePublicOccurrenceCity,
  requestBrowserCoordinates,
  type PublicOccurrencePoint,
} from './publicOccurrenceLocation';

const PAGE_SIZE = 12;
const SAO_PAULO_POINT: PublicOccurrencePoint = {
  latitude: -23.55052,
  longitude: -46.633308,
};

export function PublicOccurrences() {
  const [city, setCity] = useState(DEFAULT_PUBLIC_OCCURRENCE_CITY);
  const [radiusKm, setRadiusKm] = useState(DEFAULT_PUBLIC_OCCURRENCE_RADIUS_KM);
  const [point, setPoint] = useState<PublicOccurrencePoint | null>(null);
  const [pageData, setPageData] = useState<PublicOccurrencePage | null>(null);
  const [loading, setLoading] = useState(true);
  const [locating, setLocating] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [sourceLabel, setSourceLabel] = useState('Definindo sua localização...');

  const loadResults = useCallback(async (
    targetPoint: PublicOccurrencePoint,
    targetRadiusKm: number,
    targetPage = 1,
  ) => {
    setLoading(true);
    setError(null);
    try {
      const result = await searchPublicOccurrences({
        latitude: targetPoint.latitude,
        longitude: targetPoint.longitude,
        radiusKm: targetRadiusKm,
        page: targetPage,
        pageSize: PAGE_SIZE,
      });
      setPageData(result);
    } catch {
      setPageData(null);
      setError('Não foi possível carregar as ocorrências para esse filtro. Tente novamente.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;

    void requestBrowserCoordinates().then(async (coordinates) => {
      if (!active) return;
      setLocating(false);

      if (coordinates) {
        setPoint(coordinates);
        setCity('');
        setSourceLabel('Sua localização');
        await loadResults(coordinates, DEFAULT_PUBLIC_OCCURRENCE_RADIUS_KM, 1);
        return;
      }

      setPoint(SAO_PAULO_POINT);
      setCity(DEFAULT_PUBLIC_OCCURRENCE_CITY);
      setSourceLabel(DEFAULT_PUBLIC_OCCURRENCE_CITY);
      await loadResults(SAO_PAULO_POINT, DEFAULT_PUBLIC_OCCURRENCE_RADIUS_KM, 1);
    });

    return () => {
      active = false;
    };
  }, [loadResults]);

  async function applyCityFilter() {
    const normalizedCity = city.trim();
    if (!normalizedCity) {
      setError('Informe uma cidade ou use sua localização.');
      return;
    }
    if (!Number.isFinite(radiusKm) || radiusKm <= 0 || radiusKm > 100) {
      setError('Informe um raio entre 0,1 e 100 km.');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const coordinates = await geocodePublicOccurrenceCity(normalizedCity);
      setPoint(coordinates);
      setSourceLabel(normalizedCity);
      await loadResults(coordinates, radiusKm, 1);
    } catch (requestError) {
      setLoading(false);
      setError(requestError instanceof Error
        ? requestError.message
        : 'Não foi possível localizar a cidade informada.');
    }
  }

  async function useCurrentLocation() {
    setLocating(true);
    setError(null);
    const coordinates = await requestBrowserCoordinates();
    setLocating(false);

    if (!coordinates) {
      setError('Não foi possível obter sua localização. Autorize o navegador ou escolha uma cidade/ponto no mapa.');
      return;
    }

    setPoint(coordinates);
    setCity('');
    setSourceLabel('Sua localização');
    await loadResults(coordinates, radiusKm, 1);
  }

  function handleMapPoint(nextPoint: PublicOccurrencePoint, resolvedCity?: string) {
    setPoint(nextPoint);
    if (resolvedCity) setCity(resolvedCity);
    setSourceLabel(resolvedCity || 'Ponto escolhido no mapa');
    void loadResults(nextPoint, radiusKm, 1);
  }

  async function changePage(nextPage: number) {
    if (!point || loading) return;
    await loadResults(point, radiusKm, nextPage);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  return (
    <main className="public-occurrences">
      <section className="public-occurrences__intro">
        <div>
          <span className="public-occurrences__eyebrow">Ocorrências públicas</span>
          <h1>Acompanhe o que está acontecendo perto de você.</h1>
          <p>Consulte demandas abertas usando apenas cidade, raio ou um ponto escolhido no mapa.</p>
        </div>
        <div className="public-occurrences__location-summary">
          <span>Filtro atual</span>
          <strong>{sourceLabel}</strong>
          <small>Raio de {radiusKm} km</small>
        </div>
      </section>

      <section className="public-occurrences__workspace" aria-label="Busca pública de ocorrências">
        <aside className="public-occurrences__filters">
          <div className="public-occurrences__filter-heading">
            <span>Localização</span>
            <p>Defina uma cidade ou escolha um ponto diretamente no mapa.</p>
          </div>

          <label>
            Cidade
            <input
              value={city}
              onChange={(event) => setCity(event.target.value)}
              placeholder="Ex.: Porto Alegre"
              maxLength={120}
              disabled={loading}
            />
          </label>

          <label>
            Raio em km
            <input
              type="number"
              min="0.1"
              max="100"
              step="0.1"
              value={radiusKm}
              onChange={(event) => setRadiusKm(Number(event.target.value))}
              disabled={loading}
            />
          </label>

          <div className="public-occurrences__filter-actions">
            <Button type="button" onClick={() => void applyCityFilter()} disabled={loading || locating}>
              Aplicar cidade e raio
            </Button>
            <Button type="button" variant="soft" onClick={() => void useCurrentLocation()} disabled={loading || locating}>
              {locating ? 'Obtendo localização...' : 'Usar minha localização'}
            </Button>
          </div>

          {error && <p className="public-occurrences__error" role="alert">{error}</p>}

          <PublicOccurrenceMapFilter
            point={point}
            radiusKm={radiusKm}
            onPointChange={handleMapPoint}
          />
        </aside>

        <div className="public-occurrences__results">
          <div className="public-occurrences__results-heading">
            <div>
              <span>Resultados</span>
              <h2>{pageData?.totalItems ?? 0} ocorrência{pageData?.totalItems === 1 ? '' : 's'} encontrada{pageData?.totalItems === 1 ? '' : 's'}</h2>
            </div>
            {pageData && pageData.totalPages > 0 && (
              <small>Página {pageData.page} de {pageData.totalPages}</small>
            )}
          </div>

          {loading ? (
            <div className="public-occurrences__skeletons" aria-busy="true">
              {[0, 1, 2, 3].map((item) => <div className="public-occurrences__skeleton" key={item} />)}
            </div>
          ) : pageData && pageData.items.length > 0 ? (
            <div className="public-occurrences__list">
              {pageData.items.map((occurrence) => (
                <PublicOccurrenceCard occurrence={occurrence} key={occurrence.id} />
              ))}
            </div>
          ) : (
            <div className="public-occurrences__empty">
              <strong>Nenhuma ocorrência aberta encontrada nesse raio.</strong>
              <span>Tente aumentar o raio ou escolher outro ponto.</span>
            </div>
          )}

          {pageData && pageData.totalPages > 1 && (
            <nav className="public-occurrences__pagination" aria-label="Paginação das ocorrências">
              <Button
                type="button"
                variant="ghost"
                disabled={pageData.page <= 1 || loading}
                onClick={() => void changePage(pageData.page - 1)}
              >
                Anterior
              </Button>
              <span>{pageData.page} / {pageData.totalPages}</span>
              <Button
                type="button"
                variant="ghost"
                disabled={pageData.page >= pageData.totalPages || loading}
                onClick={() => void changePage(pageData.page + 1)}
              >
                Próxima
              </Button>
            </nav>
          )}
        </div>
      </section>
    </main>
  );
}

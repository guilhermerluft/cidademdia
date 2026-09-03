import { useState } from 'react';
import { Button } from '../../components/ui';
import { searchMyOccurrences } from './occurrenceService';
import {
  geocodePublicOccurrenceCity,
  requestBrowserCoordinates,
} from './publicOccurrenceLocation';
import type { OccurrencePage } from './types';

interface OccurrenceGeoFilterProps {
  disabled?: boolean;
  onResults: (page: OccurrencePage) => void;
  onReset: () => Promise<void>;
  onError: (message: string) => void;
}

export function OccurrenceGeoFilter({
  disabled = false,
  onResults,
  onReset,
  onError,
}: OccurrenceGeoFilterProps) {
  const [city, setCity] = useState('');
  const [radiusKm, setRadiusKm] = useState('');
  const [latitude, setLatitude] = useState<number | null>(null);
  const [longitude, setLongitude] = useState<number | null>(null);
  const [locationLabel, setLocationLabel] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [locating, setLocating] = useState(false);

  async function useCurrentLocation() {
    setLocating(true);
    const coordinates = await requestBrowserCoordinates();
    setLocating(false);

    if (!coordinates) {
      onError('Não foi possível obter sua localização. Autorize o navegador ou informe uma cidade.');
      return;
    }

    setLatitude(coordinates.latitude);
    setLongitude(coordinates.longitude);
    setCity('');
    setLocationLabel('Sua localização está pronta para o filtro por raio.');
  }

  async function applyFilters() {
    const normalizedCity = city.trim();
    const radius = radiusKm.trim() ? Number(radiusKm.replace(',', '.')) : undefined;

    if (radius !== undefined && (!Number.isFinite(radius) || radius <= 0 || radius > 100)) {
      onError('Informe um raio entre 0,1 e 100 km.');
      return;
    }

    if (!normalizedCity && radius === undefined) {
      setLoading(true);
      try {
        await onReset();
        setLocationLabel(null);
      } finally {
        setLoading(false);
      }
      return;
    }

    setLoading(true);
    try {
      let targetLatitude = latitude;
      let targetLongitude = longitude;

      if (normalizedCity && radius !== undefined) {
        const coordinates = await geocodePublicOccurrenceCity(normalizedCity);
        targetLatitude = coordinates.latitude;
        targetLongitude = coordinates.longitude;
        setLatitude(coordinates.latitude);
        setLongitude(coordinates.longitude);
        setLocationLabel(`Centro do raio: ${normalizedCity}.`);
      }

      if (radius !== undefined && (targetLatitude === null || targetLongitude === null)) {
        onError('Informe uma cidade ou use sua localização antes de aplicar o filtro por raio.');
        return;
      }

      const page = await searchMyOccurrences({
        city: normalizedCity && radius === undefined ? normalizedCity : undefined,
        latitude: radius !== undefined ? targetLatitude ?? undefined : undefined,
        longitude: radius !== undefined ? targetLongitude ?? undefined : undefined,
        radiusKm: radius,
      }, 1, 10);

      onResults(page);
    } catch (requestError) {
      onError(requestError instanceof Error
        ? requestError.message
        : 'Não foi possível aplicar os filtros geográficos. Tente novamente.');
    } finally {
      setLoading(false);
    }
  }

  async function resetFilters() {
    setCity('');
    setRadiusKm('');
    setLatitude(null);
    setLongitude(null);
    setLocationLabel(null);
    setLoading(true);
    try {
      await onReset();
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="occurrence-geo-filter" aria-label="Filtros das ocorrências publicadas por você">
      <div className="occurrence-geo-filter__fields">
        <label>
          Cidade
          <input
            value={city}
            onChange={(event) => setCity(event.target.value)}
            placeholder="Ex.: Porto Alegre"
            maxLength={120}
            disabled={disabled || loading}
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
            onChange={(event) => setRadiusKm(event.target.value)}
            placeholder="Ex.: 25"
            disabled={disabled || loading}
          />
        </label>
      </div>

      <div className="occurrence-geo-filter__toolbar">
        <div className="occurrence-geo-filter__location">
          <Button
            type="button"
            variant="soft"
            size="sm"
            onClick={() => void useCurrentLocation()}
            disabled={disabled || loading || locating}
          >
            {locating ? 'Obtendo localização...' : 'Usar minha localização'}
          </Button>
          {locationLabel ? <small>{locationLabel}</small> : null}
        </div>

        <div className="occurrence-geo-filter__actions">
          <Button type="button" size="sm" onClick={() => void applyFilters()} disabled={disabled || loading || locating}>
            {loading ? 'Filtrando...' : 'Filtrar'}
          </Button>
          <Button type="button" variant="ghost" size="sm" onClick={() => void resetFilters()} disabled={disabled || loading}>
            Limpar
          </Button>
        </div>
      </div>
    </div>
  );
}

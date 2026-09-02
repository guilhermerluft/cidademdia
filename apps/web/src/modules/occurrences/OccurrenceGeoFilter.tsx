import { useState } from 'react';
import { Button } from '../../components/ui';
import { searchMyOccurrences } from './occurrenceService';
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
  const [loading, setLoading] = useState(false);
  const [locating, setLocating] = useState(false);

  function useCurrentLocation() {
    if (!navigator.geolocation) {
      onError('Este navegador não disponibiliza geolocalização para o filtro por raio.');
      return;
    }

    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setLatitude(position.coords.latitude);
        setLongitude(position.coords.longitude);
        setLocating(false);
      },
      () => {
        setLocating(false);
        onError('Não foi possível obter sua localização para o filtro por raio.');
      },
      {
        enableHighAccuracy: true,
        timeout: 10_000,
        maximumAge: 30_000,
      },
    );
  }

  async function applyFilters() {
    const radius = radiusKm.trim() ? Number(radiusKm.replace(',', '.')) : undefined;

    if (radius !== undefined && (!Number.isFinite(radius) || radius <= 0 || radius > 100)) {
      onError('Informe um raio entre 0,1 e 100 km.');
      return;
    }

    if (radius !== undefined && (latitude === null || longitude === null)) {
      onError('Use sua localização antes de aplicar o filtro por raio.');
      return;
    }

    setLoading(true);
    try {
      const page = await searchMyOccurrences({
        city: city.trim() || undefined,
        latitude: radius !== undefined ? latitude ?? undefined : undefined,
        longitude: radius !== undefined ? longitude ?? undefined : undefined,
        radiusKm: radius,
      }, 1, 10);
      onResults(page);
    } catch {
      onError('Não foi possível aplicar os filtros geográficos. Tente novamente.');
    } finally {
      setLoading(false);
    }
  }

  async function resetFilters() {
    setCity('');
    setRadiusKm('');
    setLatitude(null);
    setLongitude(null);
    setLoading(true);
    try {
      await onReset();
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="occurrence-geo-filter" aria-label="Filtros geográficos das ocorrências">
      <label>
        Cidade
        <input
          value={city}
          onChange={(event) => setCity(event.target.value)}
          placeholder="Ex.: Porto Alegre"
          disabled={disabled || loading}
        />
      </label>

      <label>
        Raio em km
        <input
          value={radiusKm}
          onChange={(event) => setRadiusKm(event.target.value)}
          inputMode="decimal"
          placeholder="Ex.: 5"
          disabled={disabled || loading}
        />
      </label>

      <div className="occurrence-geo-filter__location">
        <Button
          type="button"
          variant="soft"
          size="sm"
          onClick={useCurrentLocation}
          disabled={disabled || loading || locating}
        >
          {locating ? 'Obtendo localização...' : latitude !== null ? 'Localização definida' : 'Usar minha localização'}
        </Button>
        {latitude !== null && longitude !== null ? (
          <small>{latitude.toFixed(4)}, {longitude.toFixed(4)}</small>
        ) : null}
      </div>

      <div className="occurrence-geo-filter__actions">
        <Button type="button" size="sm" onClick={applyFilters} disabled={disabled || loading}>
          {loading ? 'Filtrando...' : 'Filtrar'}
        </Button>
        <Button type="button" variant="ghost" size="sm" onClick={resetFilters} disabled={disabled || loading}>
          Limpar
        </Button>
      </div>
    </div>
  );
}

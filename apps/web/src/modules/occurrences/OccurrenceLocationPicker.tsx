import { useEffect, useRef, useState } from 'react';
import { Button } from '../../components/ui';

interface LocationValue {
  addressText: string;
  postalCode: string;
  stateCode: string;
  latitude: string;
  longitude: string;
}

interface OccurrenceLocationPickerProps {
  value: LocationValue;
  disabled?: boolean;
  onChange: (field: keyof LocationValue, value: string) => void;
  onError: (message: string) => void;
}

type GoogleApi = any;

declare global {
  interface Window {
    google?: GoogleApi;
    __cidademdiaGoogleMapsPromise?: Promise<GoogleApi>;
  }
}

const DEFAULT_CENTER = { lat: -30.0346, lng: -51.2177 };

function loadGoogleMaps() {
  if (window.google?.maps) return Promise.resolve(window.google);
  if (window.__cidademdiaGoogleMapsPromise) return window.__cidademdiaGoogleMapsPromise;

  const apiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY?.trim();
  if (!apiKey) {
    return Promise.reject(new Error('Google Maps não está configurado para o navegador.'));
  }

  window.__cidademdiaGoogleMapsPromise = new Promise((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>('script[data-cidademdia-google-maps]');
    if (existing) {
      existing.addEventListener('load', () => resolve(window.google));
      existing.addEventListener('error', () => reject(new Error('Falha ao carregar Google Maps.')));
      return;
    }

    const script = document.createElement('script');
    script.dataset.cidademdiaGoogleMaps = 'true';
    script.async = true;
    script.defer = true;
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places&language=pt-BR&region=BR&v=weekly`;
    script.onload = () => {
      if (window.google?.maps) resolve(window.google);
      else reject(new Error('Google Maps não inicializou corretamente.'));
    };
    script.onerror = () => reject(new Error('Falha ao carregar Google Maps.'));
    document.head.appendChild(script);
  });

  return window.__cidademdiaGoogleMapsPromise;
}

function getAddressComponent(components: any[] | undefined, type: string, shortName = false) {
  const component = components?.find((item) => item.types?.includes(type));
  if (!component) return '';
  return shortName ? component.short_name ?? '' : component.long_name ?? '';
}

export function OccurrenceLocationPicker({
  value,
  disabled = false,
  onChange,
  onError,
}: OccurrenceLocationPickerProps) {
  const addressInputRef = useRef<HTMLInputElement | null>(null);
  const mapElementRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<any>(null);
  const markerRef = useRef<any>(null);
  const geocoderRef = useRef<any>(null);
  const [mapsReady, setMapsReady] = useState(false);
  const [mapsError, setMapsError] = useState<string | null>(null);
  const [locating, setLocating] = useState(false);

  function applyGeocoderResult(result: any) {
    const location = result?.geometry?.location;
    if (!location) return;

    const latitude = location.lat();
    const longitude = location.lng();
    const postalCode = getAddressComponent(result.address_components, 'postal_code');
    const stateCode = getAddressComponent(result.address_components, 'administrative_area_level_1', true);

    onChange('addressText', result.formatted_address ?? value.addressText);
    onChange('latitude', latitude.toFixed(6));
    onChange('longitude', longitude.toFixed(6));
    if (postalCode) onChange('postalCode', postalCode);
    if (stateCode) onChange('stateCode', stateCode.toUpperCase());

    const point = { lat: latitude, lng: longitude };
    mapRef.current?.panTo(point);
    mapRef.current?.setZoom(17);
    markerRef.current?.setPosition(point);
  }

  async function reverseGeocode(latitude: number, longitude: number) {
    if (!geocoderRef.current) return;

    try {
      const response = await geocoderRef.current.geocode({
        location: { lat: latitude, lng: longitude },
      });

      const result = response?.results?.[0];
      if (result) applyGeocoderResult(result);
    } catch {
      onChange('latitude', latitude.toFixed(6));
      onChange('longitude', longitude.toFixed(6));
      onError('O ponto foi atualizado, mas o Google não conseguiu resolver o endereço. Você pode preencher o endereço manualmente.');
    }
  }

  useEffect(() => {
    let active = true;
    let mapClickListener: any;
    let markerDragListener: any;
    let placeListener: any;

    void loadGoogleMaps()
      .then((google) => {
        if (!active || !mapElementRef.current || !addressInputRef.current) return;

        const latitude = Number(value.latitude.replace(',', '.'));
        const longitude = Number(value.longitude.replace(',', '.'));
        const hasPoint = Number.isFinite(latitude) && Number.isFinite(longitude);
        const center = hasPoint ? { lat: latitude, lng: longitude } : DEFAULT_CENTER;

        const map = new google.maps.Map(mapElementRef.current, {
          center,
          zoom: hasPoint ? 17 : 12,
          streetViewControl: false,
          mapTypeControl: false,
          fullscreenControl: false,
        });

        const marker = new google.maps.Marker({
          map,
          position: center,
          draggable: true,
        });

        const geocoder = new google.maps.Geocoder();
        const autocomplete = new google.maps.places.Autocomplete(addressInputRef.current, {
          componentRestrictions: { country: 'br' },
          fields: ['address_components', 'formatted_address', 'geometry'],
          types: ['geocode'],
        });

        mapRef.current = map;
        markerRef.current = marker;
        geocoderRef.current = geocoder;

        placeListener = autocomplete.addListener('place_changed', () => {
          const place = autocomplete.getPlace();
          if (!place.geometry?.location) {
            setMapsError('Selecione um endereço sugerido pelo Google para confirmar o ponto no mapa.');
            return;
          }

          setMapsError(null);
          applyGeocoderResult(place);
        });

        mapClickListener = map.addListener('click', (event: any) => {
          if (!event.latLng) return;
          setMapsError(null);
          void reverseGeocode(event.latLng.lat(), event.latLng.lng());
        });

        markerDragListener = marker.addListener('dragend', (event: any) => {
          if (!event.latLng) return;
          setMapsError(null);
          void reverseGeocode(event.latLng.lat(), event.latLng.lng());
        });

        setMapsReady(true);
        setMapsError(null);
      })
      .catch((error: unknown) => {
        if (!active) return;
        const message = error instanceof Error ? error.message : 'Google Maps está indisponível.';
        setMapsError(`${message} Use os campos manuais abaixo.`);
      });

    return () => {
      active = false;
      placeListener?.remove?.();
      mapClickListener?.remove?.();
      markerDragListener?.remove?.();
    };
  }, []);

  useEffect(() => {
    if (!mapsReady) return;

    const latitude = Number(value.latitude.replace(',', '.'));
    const longitude = Number(value.longitude.replace(',', '.'));
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return;

    const point = { lat: latitude, lng: longitude };
    markerRef.current?.setPosition(point);
    mapRef.current?.panTo(point);
  }, [mapsReady, value.latitude, value.longitude]);

  function useCurrentLocation() {
    if (!navigator.geolocation) {
      onError('Este navegador não disponibiliza geolocalização. Informe o ponto manualmente.');
      return;
    }

    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        const latitude = position.coords.latitude;
        const longitude = position.coords.longitude;
        setLocating(false);
        setMapsError(null);

        if (mapsReady) {
          void reverseGeocode(latitude, longitude);
        } else {
          onChange('latitude', latitude.toFixed(6));
          onChange('longitude', longitude.toFixed(6));
        }
      },
      () => {
        setLocating(false);
        onError('Não foi possível obter sua localização. Autorize o navegador ou informe o ponto manualmente.');
      },
      {
        enableHighAccuracy: true,
        timeout: 10_000,
        maximumAge: 30_000,
      },
    );
  }

  return (
    <div className="occurrence-map-picker occurrence-form__full">
      <div className="occurrence-map-picker__header">
        <div>
          <span>Localização</span>
          <small>Busque o endereço, clique no mapa ou arraste o marcador para confirmar o ponto exato.</small>
        </div>
        <Button type="button" variant="soft" size="sm" onClick={useCurrentLocation} disabled={disabled || locating}>
          {locating ? 'Obtendo...' : 'Usar minha localização'}
        </Button>
      </div>

      <label className="occurrence-form__full">
        Endereço
        <input
          ref={addressInputRef}
          required
          value={value.addressText}
          onChange={(event) => onChange('addressText', event.target.value)}
          placeholder="Digite e selecione um endereço"
          autoComplete="off"
          disabled={disabled}
        />
      </label>

      <div
        ref={mapElementRef}
        className={`occurrence-map-picker__map${mapsReady ? ' is-ready' : ''}`}
        role="application"
        aria-label="Mapa para selecionar a localização da ocorrência"
      >
        {!mapsReady && !mapsError ? <span>Carregando Google Maps...</span> : null}
      </div>

      {mapsError ? <p className="occurrence-map-picker__warning">{mapsError}</p> : null}

      <div className="occurrence-map-picker__address-fields">
        <label>
          CEP
          <input
            value={value.postalCode}
            onChange={(event) => onChange('postalCode', event.target.value)}
            inputMode="numeric"
            placeholder="00000-000"
            disabled={disabled}
          />
        </label>

        <label>
          UF
          <input
            value={value.stateCode}
            onChange={(event) => onChange('stateCode', event.target.value.slice(0, 2).toUpperCase())}
            maxLength={2}
            placeholder="RS"
            disabled={disabled}
          />
        </label>
      </div>

      <details className="occurrence-map-picker__manual">
        <summary>Coordenadas do ponto</summary>
        <div className="occurrence-location__fields">
          <label>
            Latitude
            <input
              required
              inputMode="decimal"
              value={value.latitude}
              onChange={(event) => onChange('latitude', event.target.value)}
              placeholder="-30.034600"
              disabled={disabled}
            />
          </label>
          <label>
            Longitude
            <input
              required
              inputMode="decimal"
              value={value.longitude}
              onChange={(event) => onChange('longitude', event.target.value)}
              placeholder="-51.217700"
              disabled={disabled}
            />
          </label>
        </div>
      </details>
    </div>
  );
}

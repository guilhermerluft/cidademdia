import { useEffect, useRef, useState } from 'react';
import { Button } from '../../components/ui';
import { geocodeGoogleAddress, loadGoogleMaps, readGoogleCoordinate } from '../../services/googleMaps';

interface LocationValue {
  addressText: string;
  street: string;
  number: string;
  neighborhood: string;
  city: string;
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

const DEFAULT_CENTER = { lat: -30.0346, lng: -51.2177 };

function getAddressComponent(components: any[] | undefined, type: string, shortName = false) {
  const component = components?.find((item) => item.types?.includes(type));
  if (!component) return '';

  if (shortName) {
    return component.shortText ?? component.short_name ?? '';
  }

  return component.longText ?? component.long_name ?? '';
}

function buildAddressQuery(value: LocationValue) {
  const cityState = [value.city.trim(), value.stateCode.trim().toUpperCase()]
    .filter(Boolean)
    .join(' - ');

  return [
    [value.street.trim(), value.number.trim()].filter(Boolean).join(', '),
    value.neighborhood.trim(),
    cityState,
    value.postalCode.trim(),
    'Brasil',
  ].filter(Boolean).join(', ');
}

export function OccurrenceLocationPicker({
  value,
  disabled = false,
  onChange,
  onError,
}: OccurrenceLocationPickerProps) {
  const autocompleteHostRef = useRef<HTMLDivElement | null>(null);
  const autocompleteRef = useRef<any>(null);
  const mapElementRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<any>(null);
  const markerRef = useRef<any>(null);
  const geocoderRef = useRef<any>(null);
  const [mapsReady, setMapsReady] = useState(false);
  const [mapsError, setMapsError] = useState<string | null>(null);
  const [locating, setLocating] = useState(false);
  const [searchingAddress, setSearchingAddress] = useState(false);

  function applyLocationResult(result: any, preserveMissingParts = false) {
    const location = result?.location ?? result?.geometry?.location;
    if (!location) return;

    const latitude = readGoogleCoordinate(location, 'lat');
    const longitude = readGoogleCoordinate(location, 'lng');
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return;

    const components = result.addressComponents ?? result.address_components;
    const street = getAddressComponent(components, 'route');
    const number = getAddressComponent(components, 'street_number');
    const neighborhood = getAddressComponent(components, 'sublocality_level_1')
      || getAddressComponent(components, 'neighborhood')
      || getAddressComponent(components, 'sublocality');
    const city = getAddressComponent(components, 'locality')
      || getAddressComponent(components, 'administrative_area_level_2');
    const postalCode = getAddressComponent(components, 'postal_code');
    const stateCode = getAddressComponent(components, 'administrative_area_level_1', true);
    const formattedAddress = result.formattedAddress ?? result.formatted_address ?? value.addressText;

    if (!preserveMissingParts || street) onChange('street', street);
    if (!preserveMissingParts || number) onChange('number', number);
    if (!preserveMissingParts || neighborhood) onChange('neighborhood', neighborhood);
    if (!preserveMissingParts || city) onChange('city', city);
    if (!preserveMissingParts || postalCode) onChange('postalCode', postalCode);
    if (!preserveMissingParts || stateCode) onChange('stateCode', stateCode.toUpperCase());
    onChange('addressText', formattedAddress);
    onChange('latitude', latitude.toFixed(6));
    onChange('longitude', longitude.toFixed(6));

    if (autocompleteRef.current && typeof formattedAddress === 'string') {
      autocompleteRef.current.value = formattedAddress;
    }

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
      if (result) {
        applyLocationResult(result);
        return;
      }

      onChange('latitude', latitude.toFixed(6));
      onChange('longitude', longitude.toFixed(6));
      onError('O ponto foi atualizado, mas o endereço não pôde ser preenchido automaticamente. Complete os campos obrigatórios.');
    } catch {
      onChange('latitude', latitude.toFixed(6));
      onChange('longitude', longitude.toFixed(6));
      onError('O ponto foi atualizado, mas o Google não conseguiu resolver o endereço. Complete os campos obrigatórios.');
    }
  }

  useEffect(() => {
    let active = true;
    let mapClickListener: any;
    let markerDragListener: any;
    let autocompleteInputListener: ((event: Event) => void) | undefined;
    let autocompleteSelectListener: ((event: Event) => void) | undefined;
    let autocompleteErrorListener: ((event: Event) => void) | undefined;
    let autocomplete: any;

    void loadGoogleMaps()
      .then(async (google) => {
        if (!active || !mapElementRef.current || !autocompleteHostRef.current) return;

        const placesLibrary = await google.maps.importLibrary('places');
        const PlaceAutocompleteElement = placesLibrary?.PlaceAutocompleteElement;
        if (!PlaceAutocompleteElement) {
          throw new Error('Google Places API (New) não está disponível para esta chave.');
        }

        const latitudeText = value.latitude.trim();
        const longitudeText = value.longitude.trim();
        const latitude = Number(latitudeText.replace(',', '.'));
        const longitude = Number(longitudeText.replace(',', '.'));
        const hasPoint = latitudeText.length > 0
          && longitudeText.length > 0
          && Number.isFinite(latitude)
          && Number.isFinite(longitude);
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
          draggable: true,
        });
        if (hasPoint) marker.setPosition(center);

        const geocoder = new google.maps.Geocoder();
        autocomplete = new PlaceAutocompleteElement({
          includedRegionCodes: ['br'],
          placeholder: 'Digite e selecione um endereço',
          requestedLanguage: 'pt-BR',
          requestedRegion: 'BR',
        });
        autocomplete.className = 'occurrence-map-picker__places-widget';
        autocomplete.disabled = disabled;
        autocomplete.value = value.addressText;

        autocompleteInputListener = () => {
          if (typeof autocomplete.value === 'string') {
            onChange('addressText', autocomplete.value);
          }
        };

        autocompleteSelectListener = (event: Event) => {
          void (async () => {
            try {
              const placePrediction = (event as any).placePrediction;
              const place = placePrediction?.toPlace?.();
              if (!place) {
                setMapsError('Não foi possível identificar o endereço selecionado. Use os campos abaixo.');
                return;
              }

              await place.fetchFields({
                fields: ['formattedAddress', 'location', 'addressComponents'],
              });

              if (!place.location) {
                setMapsError('Selecione um endereço com localização válida para confirmar o ponto no mapa.');
                return;
              }

              setMapsError(null);
              applyLocationResult(place);
            } catch {
              setMapsError('O Google Places não conseguiu carregar os detalhes do endereço. Use os campos abaixo.');
            }
          })();
        };

        autocompleteErrorListener = () => {
          setMapsError('O Google Places não conseguiu buscar endereços. Você ainda pode preencher os campos e usar “Buscar no mapa”.');
        };

        autocomplete.addEventListener('input', autocompleteInputListener);
        autocomplete.addEventListener('gmp-select', autocompleteSelectListener);
        autocomplete.addEventListener('gmp-error', autocompleteErrorListener);
        autocompleteHostRef.current.appendChild(autocomplete);

        mapRef.current = map;
        markerRef.current = marker;
        geocoderRef.current = geocoder;

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
        setMapsError(`${message} Preencha os campos obrigatórios abaixo.`);
      });

    return () => {
      active = false;
      mapClickListener?.remove?.();
      markerDragListener?.remove?.();

      if (autocomplete) {
        if (autocompleteInputListener) autocomplete.removeEventListener('input', autocompleteInputListener);
        if (autocompleteSelectListener) autocomplete.removeEventListener('gmp-select', autocompleteSelectListener);
        if (autocompleteErrorListener) autocomplete.removeEventListener('gmp-error', autocompleteErrorListener);
        autocomplete.remove?.();
      }

      autocompleteRef.current = null;
    };
  }, []);

  useEffect(() => {
    if (!mapsReady) return;

    const latitudeText = value.latitude.trim();
    const longitudeText = value.longitude.trim();
    if (!latitudeText || !longitudeText) return;

    const latitude = Number(latitudeText.replace(',', '.'));
    const longitude = Number(longitudeText.replace(',', '.'));
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return;

    const point = { lat: latitude, lng: longitude };
    markerRef.current?.setPosition(point);
    mapRef.current?.panTo(point);
  }, [mapsReady, value.latitude, value.longitude]);

  useEffect(() => {
    const autocomplete = autocompleteRef.current;
    if (!autocomplete) return;

    autocomplete.disabled = disabled;
    if (typeof value.addressText === 'string' && autocomplete.value !== value.addressText) {
      autocomplete.value = value.addressText;
    }
  }, [disabled, value.addressText]);

  function useCurrentLocation() {
    if (!navigator.geolocation) {
      onError('Este navegador não disponibiliza geolocalização. Informe o endereço manualmente.');
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
        onError('Não foi possível obter sua localização. Autorize o navegador ou informe o endereço manualmente.');
      },
      {
        enableHighAccuracy: true,
        timeout: 10_000,
        maximumAge: 30_000,
      },
    );
  }

  async function searchManualAddress() {
    setSearchingAddress(true);
    setMapsError(null);
    try {
      const { result } = await geocodeGoogleAddress(buildAddressQuery(value));
      applyLocationResult(result, true);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Não foi possível localizar o endereço informado.';
      setMapsError(message);
      onError(message);
    } finally {
      setSearchingAddress(false);
    }
  }

  const canSearchAddress = Boolean(
    value.street.trim()
    && value.number.trim()
    && value.neighborhood.trim()
    && value.city.trim(),
  );

  return (
    <div className="occurrence-map-picker occurrence-form__full">
      <div className="occurrence-map-picker__header">
        <div>
          <span>Localização</span>
          <small>Busque um endereço, clique no mapa ou arraste o marcador. Rua, número, bairro e cidade são obrigatórios.</small>
        </div>
        <Button type="button" variant="soft" size="sm" onClick={useCurrentLocation} disabled={disabled || locating}>
          {locating ? 'Obtendo...' : 'Usar minha localização'}
        </Button>
      </div>

      <label className="occurrence-form__full">
        Buscar endereço
        <div
          ref={autocompleteHostRef}
          className="occurrence-map-picker__autocomplete"
          aria-busy={!mapsReady}
          hidden={Boolean(mapsError && !mapsReady)}
        />
        {!mapsReady && !mapsError ? <span>Carregando busca de endereços...</span> : null}
      </label>

      <div className="occurrence-map-picker__structured-address">
        <label>
          Rua <span className="occurrence-required-marker" aria-hidden="true">*</span>
          <input
            required
            value={value.street}
            onChange={(event) => onChange('street', event.target.value)}
            autoComplete="address-line1"
            disabled={disabled}
          />
        </label>

        <label>
          Número <span className="occurrence-required-marker" aria-hidden="true">*</span>
          <input
            required
            value={value.number}
            onChange={(event) => onChange('number', event.target.value)}
            autoComplete="address-line2"
            disabled={disabled}
          />
        </label>

        <label>
          Bairro <span className="occurrence-required-marker" aria-hidden="true">*</span>
          <input
            required
            value={value.neighborhood}
            onChange={(event) => onChange('neighborhood', event.target.value)}
            disabled={disabled}
          />
        </label>

        <label>
          Cidade <span className="occurrence-required-marker" aria-hidden="true">*</span>
          <input
            required
            value={value.city}
            onChange={(event) => onChange('city', event.target.value)}
            autoComplete="address-level2"
            disabled={disabled}
          />
        </label>

        <label>
          CEP
          <input
            value={value.postalCode}
            onChange={(event) => onChange('postalCode', event.target.value)}
            inputMode="numeric"
            placeholder="00000-000"
            autoComplete="postal-code"
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
            autoComplete="address-level1"
            disabled={disabled}
          />
        </label>
      </div>

      <div className="occurrence-map-picker__address-action">
        <Button
          type="button"
          variant="soft"
          size="sm"
          onClick={() => void searchManualAddress()}
          disabled={disabled || searchingAddress || !canSearchAddress}
        >
          {searchingAddress ? 'Buscando endereço...' : 'Buscar no mapa'}
        </Button>
        <small>Ao buscar, o marcador é reposicionado. Ao mover o marcador, os campos são preenchidos novamente pelo Google.</small>
      </div>

      <div
        ref={mapElementRef}
        className={`occurrence-map-picker__map${mapsReady ? ' is-ready' : ''}`}
        role="application"
        aria-label="Mapa para selecionar a localização da ocorrência"
      />
      {!mapsReady && !mapsError ? <span>Carregando Google Maps...</span> : null}

      {mapsError ? <p className="occurrence-map-picker__warning">{mapsError}</p> : null}

      <details className="occurrence-map-picker__manual">
        <summary>Coordenadas do ponto</summary>
        <div className="occurrence-location__fields">
          <label>
            Latitude
            <input
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

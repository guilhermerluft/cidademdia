import { useEffect, useRef, useState } from 'react';
import { loadGoogleMaps } from '../../services/googleMaps';
import type { PublicOccurrencePoint } from './publicOccurrenceLocation';

interface PublicOccurrenceMapFilterProps {
  point: PublicOccurrencePoint | null;
  radiusKm: number;
  onPointChange: (point: PublicOccurrencePoint, city?: string) => void;
}

const SAO_PAULO_CENTER = { lat: -23.55052, lng: -46.633308 };

function getCityFromResult(result: any) {
  const components = result?.address_components ?? result?.addressComponents ?? [];
  const cityComponent = components.find((component: any) =>
    component.types?.includes('administrative_area_level_2')
    || component.types?.includes('locality'));
  return cityComponent?.long_name ?? cityComponent?.longText ?? '';
}

export function PublicOccurrenceMapFilter({
  point,
  radiusKm,
  onPointChange,
}: PublicOccurrenceMapFilterProps) {
  const mapElementRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<any>(null);
  const markerRef = useRef<any>(null);
  const circleRef = useRef<any>(null);
  const geocoderRef = useRef<any>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    let mapClickListener: any;
    let markerDragListener: any;

    void loadGoogleMaps()
      .then((google) => {
        if (!active || !mapElementRef.current) return;

        const center = point
          ? { lat: point.latitude, lng: point.longitude }
          : SAO_PAULO_CENTER;
        const map = new google.maps.Map(mapElementRef.current, {
          center,
          zoom: 11,
          streetViewControl: false,
          mapTypeControl: false,
          fullscreenControl: false,
          clickableIcons: false,
        });
        const marker = new google.maps.Marker({
          map,
          position: center,
          draggable: true,
          title: 'Centro do filtro de ocorrências',
        });
        const circle = new google.maps.Circle({
          map,
          center,
          radius: radiusKm * 1000,
          clickable: false,
          fillOpacity: 0.08,
          strokeOpacity: 0.55,
          strokeWeight: 2,
        });
        const geocoder = new google.maps.Geocoder();

        async function choosePoint(latitude: number, longitude: number) {
          let city = '';
          try {
            const response = await geocoder.geocode({ location: { lat: latitude, lng: longitude } });
            city = getCityFromResult(response?.results?.[0]);
          } catch {
            // O ponto continua válido mesmo quando o reverse geocode falha.
          }
          if (active) onPointChange({ latitude, longitude }, city || undefined);
        }

        mapClickListener = map.addListener('click', (event: any) => {
          if (!event.latLng) return;
          void choosePoint(event.latLng.lat(), event.latLng.lng());
        });
        markerDragListener = marker.addListener('dragend', (event: any) => {
          if (!event.latLng) return;
          void choosePoint(event.latLng.lat(), event.latLng.lng());
        });

        mapRef.current = map;
        markerRef.current = marker;
        circleRef.current = circle;
        geocoderRef.current = geocoder;
        setError(null);
      })
      .catch((requestError: unknown) => {
        if (!active) return;
        setError(requestError instanceof Error ? requestError.message : 'Mapa indisponível.');
      });

    return () => {
      active = false;
      mapClickListener?.remove?.();
      markerDragListener?.remove?.();
    };
  }, []);

  useEffect(() => {
    if (!point) return;
    const nextPoint = { lat: point.latitude, lng: point.longitude };
    markerRef.current?.setPosition(nextPoint);
    circleRef.current?.setCenter(nextPoint);
    mapRef.current?.panTo(nextPoint);
  }, [point]);

  useEffect(() => {
    circleRef.current?.setRadius(radiusKm * 1000);
  }, [radiusKm]);

  return (
    <div className="public-occurrences__map-wrap">
      <div
        ref={mapElementRef}
        className="public-occurrences__map"
        role="application"
        aria-label="Mapa para escolher o centro do filtro de ocorrências"
      />
      {error && <p className="public-occurrences__map-error">{error}</p>}
      <p className="public-occurrences__map-help">
        Clique no mapa ou arraste o marcador para usar outro ponto como centro do raio.
      </p>
    </div>
  );
}

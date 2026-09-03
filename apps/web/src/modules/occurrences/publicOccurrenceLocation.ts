import { loadGoogleMaps, readGoogleCoordinate } from '../../services/googleMaps';

export const DEFAULT_PUBLIC_OCCURRENCE_CITY = 'São Paulo';
export const DEFAULT_PUBLIC_OCCURRENCE_RADIUS_KM = 25;

export interface PublicOccurrencePoint {
  latitude: number;
  longitude: number;
}

export async function requestBrowserCoordinates(): Promise<PublicOccurrencePoint | null> {
  if (!('geolocation' in navigator)) return null;

  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      (position) => resolve({
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
      }),
      () => resolve(null),
      { enableHighAccuracy: false, timeout: 5500, maximumAge: 10 * 60 * 1000 },
    );
  });
}

export async function geocodePublicOccurrenceCity(city: string): Promise<PublicOccurrencePoint> {
  const normalized = city.trim();
  if (!normalized) throw new Error('Informe uma cidade.');

  const google = await loadGoogleMaps();
  const geocoder = new google.maps.Geocoder();
  const response = await geocoder.geocode({
    address: `${normalized}, Brasil`,
    componentRestrictions: { country: 'BR' },
  });
  const result = response?.results?.[0];
  const location = result?.geometry?.location;
  const latitude = readGoogleCoordinate(location, 'lat');
  const longitude = readGoogleCoordinate(location, 'lng');

  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
    throw new Error('Não foi possível localizar essa cidade no mapa.');
  }

  return { latitude, longitude };
}

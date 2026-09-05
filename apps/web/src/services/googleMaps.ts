type GoogleApi = any;

declare global {
  interface Window {
    google?: GoogleApi;
    __cidademdiaGoogleMapsPromise?: Promise<GoogleApi>;
  }
}

function installGoogleMapsBootstrap(apiKey: string) {
  const win = window as any;
  const google = win.google ?? (win.google = {});
  const maps = google.maps ?? (google.maps = {});

  if (typeof maps.importLibrary === 'function') return;

  let loadPromise: Promise<void> | undefined;
  const requestedLibraries = new Set<string>();
  const callbackName = '__cidademdiaGoogleMapsInit';

  const load = () => {
    if (loadPromise) return loadPromise;

    loadPromise = new Promise<void>((resolve, reject) => {
      const params = new URLSearchParams();
      params.set('libraries', [...requestedLibraries].join(','));
      params.set('key', apiKey);
      params.set('v', 'weekly');
      params.set('loading', 'async');
      params.set('language', 'pt-BR');
      params.set('region', 'BR');
      params.set('callback', `google.maps.${callbackName}`);

      maps[callbackName] = resolve;

      const script = document.createElement('script');
      script.dataset.cidademdiaGoogleMaps = 'true';
      script.async = true;
      script.src = `https://maps.googleapis.com/maps/api/js?${params.toString()}`;
      script.onerror = () => {
        loadPromise = undefined;
        reject(new Error('Falha ao carregar Google Maps.'));
      };
      script.nonce = document.querySelector<HTMLScriptElement>('script[nonce]')?.nonce ?? '';
      document.head.appendChild(script);
    });

    return loadPromise;
  };

  maps.importLibrary = (libraryName: string, ...args: any[]) => {
    requestedLibraries.add(libraryName);
    return load().then(() => maps.importLibrary(libraryName, ...args));
  };
}

export async function loadGoogleMaps() {
  if (window.google?.maps?.importLibrary && window.google?.maps?.Map) {
    return window.google;
  }
  if (window.__cidademdiaGoogleMapsPromise) return window.__cidademdiaGoogleMapsPromise;

  const apiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY?.trim();
  if (!apiKey) {
    throw new Error('Google Maps não está configurado para o navegador.');
  }

  installGoogleMapsBootstrap(apiKey);

  window.__cidademdiaGoogleMapsPromise = Promise.all([
    window.google!.maps.importLibrary('maps'),
    window.google!.maps.importLibrary('places'),
    window.google!.maps.importLibrary('geocoding'),
  ]).then(() => {
    if (!window.google?.maps?.Map || !window.google?.maps?.Geocoder) {
      throw new Error('Google Maps não inicializou corretamente.');
    }

    return window.google;
  });

  return window.__cidademdiaGoogleMapsPromise;
}

export function readGoogleCoordinate(location: any, coordinate: 'lat' | 'lng') {
  const value = location?.[coordinate];
  return typeof value === 'function' ? value.call(location) : Number(value);
}

export async function geocodeGoogleAddress(address: string) {
  const normalized = address.trim();
  if (!normalized) {
    throw new Error('Informe o endereço completo antes de localizar o ponto no mapa.');
  }

  const google = await loadGoogleMaps();
  const geocoder = new google.maps.Geocoder();
  const response = await geocoder.geocode({
    address: normalized,
    componentRestrictions: { country: 'BR' },
  });
  const result = response?.results?.[0];
  const location = result?.geometry?.location ?? result?.location;

  if (!result || !location) {
    throw new Error('Não foi possível localizar o endereço informado no mapa.');
  }

  const latitude = readGoogleCoordinate(location, 'lat');
  const longitude = readGoogleCoordinate(location, 'lng');
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
    throw new Error('O endereço informado não retornou coordenadas válidas.');
  }

  return { result, latitude, longitude };
}

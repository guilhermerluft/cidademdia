export type CommercialSignupIntent = 'support' | 'details';

const EVENT_NAME = 'cidademdia:commercial-signup';

export function requestCommercialSignup(intent: CommercialSignupIntent) {
  window.dispatchEvent(new CustomEvent<CommercialSignupIntent>(EVENT_NAME, { detail: intent }));
}

export function subscribeCommercialSignup(
  listener: (intent: CommercialSignupIntent) => void,
) {
  const handler = (event: Event) => {
    const customEvent = event as CustomEvent<CommercialSignupIntent>;
    listener(customEvent.detail);
  };

  window.addEventListener(EVENT_NAME, handler);
  return () => window.removeEventListener(EVENT_NAME, handler);
}

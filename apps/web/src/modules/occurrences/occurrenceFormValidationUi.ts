const FORM_SELECTOR = '.occurrence-form';
const VALIDATION_VISIBLE_CLASS = 'occurrence-form--validation-visible';
const INVALID_CONTROL_CLASS = 'occurrence-required-input-invalid';
const INVALID_FIELD_CLASS = 'occurrence-required-invalid';

function syncRequiredControls(form: HTMLFormElement) {
  const showValidation = form.classList.contains(VALIDATION_VISIBLE_CLASS);

  form.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>('[required]')
    .forEach((control) => {
      const invalid = showValidation && !control.checkValidity();
      const field = control.closest<HTMLElement>('label');

      control.classList.toggle(INVALID_CONTROL_CLASS, invalid);
      field?.classList.toggle(INVALID_FIELD_CLASS, invalid);

      if (invalid) {
        control.setAttribute('aria-invalid', 'true');
      } else {
        control.removeAttribute('aria-invalid');
      }
    });

  const mediaInput = form.querySelector<HTMLInputElement>('.occurrence-media-field input[type="file"]');
  const mediaField = mediaInput?.closest<HTMLElement>('.occurrence-media-field');

  if (mediaInput && mediaField) {
    const hasImage = Array.from(mediaInput.files ?? []).some((file) => file.type.startsWith('image/'));
    const invalid = showValidation && !hasImage;

    mediaInput.classList.toggle(INVALID_CONTROL_CLASS, invalid);
    mediaField.classList.toggle(INVALID_FIELD_CLASS, invalid);

    if (invalid) {
      mediaInput.setAttribute('aria-invalid', 'true');
    } else {
      mediaInput.removeAttribute('aria-invalid');
    }
  }
}

function focusFirstInvalidControl(form: HTMLFormElement) {
  const firstInvalidControl = form.querySelector<HTMLElement>(`.${INVALID_CONTROL_CLASS}`);
  if (!firstInvalidControl) return;

  requestAnimationFrame(() => {
    firstInvalidControl.scrollIntoView({ behavior: 'smooth', block: 'center' });
    firstInvalidControl.focus({ preventScroll: true });
  });
}

function clearValidationState(form: HTMLFormElement) {
  form.classList.remove(VALIDATION_VISIBLE_CLASS);

  form.querySelectorAll<HTMLElement>(`.${INVALID_CONTROL_CLASS}, .${INVALID_FIELD_CLASS}`)
    .forEach((element) => {
      element.classList.remove(INVALID_CONTROL_CLASS, INVALID_FIELD_CLASS);
      element.removeAttribute('aria-invalid');
    });
}

export function installOccurrenceFormValidationUi() {
  document.addEventListener('submit', (event) => {
    const form = event.target instanceof HTMLFormElement && event.target.matches(FORM_SELECTOR)
      ? event.target
      : null;

    if (!form) return;

    form.classList.add(VALIDATION_VISIBLE_CLASS);
    syncRequiredControls(form);
    focusFirstInvalidControl(form);
  }, true);

  const syncFromControlEvent = (event: Event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) return;

    const form = target.closest<HTMLFormElement>(FORM_SELECTOR);
    if (!form || !form.classList.contains(VALIDATION_VISIBLE_CLASS)) return;

    syncRequiredControls(form);
  };

  document.addEventListener('input', syncFromControlEvent, true);
  document.addEventListener('change', syncFromControlEvent, true);

  const observer = new MutationObserver(() => {
    document.querySelectorAll<HTMLFormElement>(FORM_SELECTOR).forEach((form) => {
      if (form.querySelector('.occurrence-success')) {
        clearValidationState(form);
      }
    });
  });

  observer.observe(document.body, {
    childList: true,
    subtree: true,
  });
}

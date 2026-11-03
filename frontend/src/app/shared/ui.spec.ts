import { HttpErrorResponse } from '@angular/common/http';
import { errorMessages } from '../core/api';
import { QarPipe } from './ui';

describe('QarPipe', () => {
  const pipe = new QarPipe();

  it('formats whole amounts without decimals', () => {
    expect(pipe.transform(3450)).toBe('QAR 3,450');
  });

  it('keeps two decimals for fractional amounts', () => {
    expect(pipe.transform(723.6)).toBe('QAR 723.60');
  });

  it('returns an empty string for missing values', () => {
    expect(pipe.transform(null)).toBe('');
  });
});

describe('errorMessages', () => {
  it('flattens FluentValidation field errors from the API', () => {
    const err = new HttpErrorResponse({
      status: 400,
      error: { title: 'Validation failed', errors: { ContactEmail: ['Enter a valid email.'], Travellers: ['Add a traveller.'] } },
    });
    expect(errorMessages(err)).toEqual(['Enter a valid email.', 'Add a traveller.']);
  });

  it('uses the ProblemDetails title for business rule errors', () => {
    const err = new HttpErrorResponse({ status: 422, error: { title: 'Only 2 seat(s) left on this departure.' } });
    expect(errorMessages(err)).toEqual(['Only 2 seat(s) left on this departure.']);
  });

  it('explains rate limiting', () => {
    expect(errorMessages(new HttpErrorResponse({ status: 429 }))[0]).toContain('Too many attempts');
  });
});

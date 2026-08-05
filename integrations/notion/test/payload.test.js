import test from 'node:test';
import assert from 'node:assert/strict';

import { validatePayload, coerceNumber, coerceString, DIRECTIONS } from '../src/payload.js';

/** A payload shaped exactly like the one ATCS_strategy.pine emits on entry. */
const validEntry = () => ({
  event: 'entry',
  secret: 'super-secret',
  symbol: 'OANDA:XAUUSD',
  timeframe: '15',
  direction: 'long',
  price: '4512.5',
  entry: '4508.25',
  stop: '4496.8',
  target: '4551.4',
  quality: '82',
  session: 'LONDON',
  note: 'LONG A: SSL swept + bullish displacement + MSS + discount -> draw BSL @ 4551.40',
  time: '2026-08-05T13:45:00Z',
});

test('a valid entry payload passes', () => {
  const result = validatePayload(validEntry());
  assert.equal(result.ok, true);
  assert.equal(result.value.event, 'entry');
  assert.equal(result.value.symbol, 'OANDA:XAUUSD');
  assert.equal(result.value.timeframe, '15');
  assert.equal(result.value.direction, 'long');
  assert.equal(result.value.session, 'LONDON');
  assert.equal(result.value.time, '2026-08-05T13:45:00.000Z');
});

test('the secret never survives validation', () => {
  const result = validatePayload(validEntry());
  assert.equal(result.ok, true);
  assert.equal('secret' in result.value, false);
});

test('a minimal payload (required fields only) passes', () => {
  const result = validatePayload({
    event: 'sweep',
    symbol: 'BINANCE:BTCUSDT',
    timeframe: '60',
    direction: 'short',
    price: 68120.5,
  });
  assert.equal(result.ok, true);
  assert.equal(result.value.price, 68120.5);
  assert.equal('entry' in result.value, false);
  assert.equal('note' in result.value, false);
});

for (const field of ['event', 'symbol', 'timeframe', 'direction', 'price']) {
  test(`a missing "${field}" is rejected`, () => {
    const body = validEntry();
    delete body[field];
    const result = validatePayload(body);
    assert.equal(result.ok, false);
    assert.ok(
      result.errors.some((e) => e.startsWith(`${field} is required`)),
      `expected a "${field} is required" error, got ${JSON.stringify(result.errors)}`,
    );
  });
}

test('every missing required field is reported at once', () => {
  const result = validatePayload({});
  assert.equal(result.ok, false);
  assert.equal(result.errors.length, 5);
});

test('a bad direction value is rejected', () => {
  const result = validatePayload({ ...validEntry(), direction: 'sideways' });
  assert.equal(result.ok, false);
  assert.ok(result.errors.some((e) => e.includes('direction must be one of')));
  assert.ok(result.errors.some((e) => e.includes(DIRECTIONS.join(' | '))));
});

test('direction is accepted case-insensitively and normalised to lowercase', () => {
  for (const input of ['LONG', 'Long', ' lOnG ']) {
    const result = validatePayload({ ...validEntry(), direction: input });
    assert.equal(result.ok, true, `expected "${input}" to be accepted`);
    assert.equal(result.value.direction, 'long');
  }
  assert.equal(validatePayload({ ...validEntry(), direction: 'SHORT' }).value.direction, 'short');
});

test('numeric strings are coerced to numbers (TradingView sends strings)', () => {
  const result = validatePayload(validEntry());
  assert.equal(result.ok, true);
  assert.equal(result.value.price, 4512.5);
  assert.equal(result.value.entry, 4508.25);
  assert.equal(result.value.stop, 4496.8);
  assert.equal(result.value.target, 4551.4);
  assert.equal(result.value.quality, 82);
  for (const key of ['price', 'entry', 'stop', 'target', 'quality']) {
    assert.equal(typeof result.value[key], 'number', `${key} should be a number`);
  }
});

test('a non-numeric price is rejected', () => {
  const result = validatePayload({ ...validEntry(), price: 'NaN' });
  assert.equal(result.ok, false);
  assert.ok(result.errors.some((e) => e.includes('price must be a number')));
});

test('a non-numeric optional number is rejected', () => {
  const result = validatePayload({ ...validEntry(), quality: 'high' });
  assert.equal(result.ok, false);
  assert.ok(result.errors.some((e) => e.includes('quality must be a number when present')));
});

test('an absent time is stamped with receipt time', () => {
  const body = validEntry();
  delete body.time;
  const now = new Date('2026-08-05T12:00:00.000Z');
  const result = validatePayload(body, { now });
  assert.equal(result.ok, true);
  assert.equal(result.value.time, '2026-08-05T12:00:00.000Z');
  assert.equal(result.value.timeStamped, true);
});

test('an unparseable time is rejected', () => {
  const result = validatePayload({ ...validEntry(), time: 'last tuesday' });
  assert.equal(result.ok, false);
  assert.ok(result.errors.some((e) => e.includes('time must be an ISO-8601 timestamp')));
});

test('a non-object body is rejected', () => {
  for (const body of [null, 'entry', 42, ['entry']]) {
    const result = validatePayload(body);
    assert.equal(result.ok, false);
    assert.deepEqual(result.errors, ['body must be a JSON object']);
  }
});

test('coerceNumber distinguishes absent from invalid', () => {
  assert.equal(coerceNumber(undefined), undefined);
  assert.equal(coerceNumber(null), undefined);
  assert.equal(coerceNumber(''), undefined);
  assert.equal(coerceNumber('   '), undefined);
  assert.equal(coerceNumber('12.5'), 12.5);
  assert.equal(coerceNumber(-3), -3);
  assert.equal(coerceNumber('abc'), null);
  assert.equal(coerceNumber(Number.NaN), null);
  assert.equal(coerceNumber(Infinity), null);
  assert.equal(coerceNumber({}), null);
});

test('coerceString trims, and treats blank as absent', () => {
  assert.equal(coerceString('  15 '), '15');
  assert.equal(coerceString('   '), undefined);
  assert.equal(coerceString(undefined), undefined);
});

test('over-long text is capped at the Notion limit', () => {
  const result = validatePayload({ ...validEntry(), note: 'x'.repeat(5000) });
  assert.equal(result.ok, true);
  assert.equal(result.value.note.length, 2000);
});

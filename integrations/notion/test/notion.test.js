import test from 'node:test';
import assert from 'node:assert/strict';

import { validatePayload } from '../src/payload.js';
import {
  PROPERTY_NAMES,
  PROPERTY_TYPES,
  buildTitle,
  buildNotionPage,
  toNotionProperties,
} from '../src/notion.js';

const DATABASE_ID = '1f0a2b3c4d5e6f708192a3b4c5d6e7f8';

const normalise = (body) => {
  const result = validatePayload(body);
  assert.equal(result.ok, true, `expected a valid payload, got ${JSON.stringify(result.errors)}`);
  return result.value;
};

const fullEntry = () =>
  normalise({
    event: 'entry',
    symbol: 'OANDA:XAUUSD',
    timeframe: '15',
    direction: 'long',
    price: '4512.5',
    entry: '4508.25',
    stop: '4496.8',
    target: '4551.4',
    quality: '82',
    session: 'LONDON',
    note: 'LONG A: SSL swept + bullish displacement + MSS + discount',
    time: '2026-08-05T13:45:00Z',
  });

test('the title is "{SYMBOL} {DIRECTION} {EVENT}"', () => {
  assert.equal(buildTitle(fullEntry()), 'OANDA:XAUUSD LONG ENTRY');
});

test('a representative payload maps to the expected Notion page shape', () => {
  const page = buildNotionPage(fullEntry(), DATABASE_ID);

  assert.deepEqual(page, {
    parent: { database_id: DATABASE_ID },
    properties: {
      Name: { title: [{ type: 'text', text: { content: 'OANDA:XAUUSD LONG ENTRY' } }] },
      Event: { select: { name: 'entry' } },
      Symbol: { rich_text: [{ type: 'text', text: { content: 'OANDA:XAUUSD' } }] },
      Timeframe: { select: { name: '15' } },
      Direction: { select: { name: 'long' } },
      Price: { number: 4512.5 },
      Entry: { number: 4508.25 },
      Stop: { number: 4496.8 },
      Target: { number: 4551.4 },
      Quality: { number: 82 },
      Session: { select: { name: 'LONDON' } },
      Note: {
        rich_text: [
          {
            type: 'text',
            text: { content: 'LONG A: SSL swept + bullish displacement + MSS + discount' },
          },
        ],
      },
      'Signal Time': { date: { start: '2026-08-05T13:45:00.000Z' } },
    },
  });
});

test('absent optional properties are omitted, never sent as null', () => {
  const payload = normalise({
    event: 'exit',
    symbol: 'BINANCE:BTCUSDT',
    timeframe: '60',
    direction: 'short',
    price: 68120.5,
    time: '2026-08-05T13:45:00Z',
  });
  const props = toNotionProperties(payload);

  for (const key of ['Entry', 'Stop', 'Target', 'Quality', 'Session', 'Note']) {
    assert.equal(key in props, false, `${key} should be absent`);
  }
  const serialised = JSON.stringify(props);
  assert.equal(serialised.includes('null'), false, 'no property may serialise to null');

  // Required properties are still there.
  for (const key of ['Name', 'Event', 'Symbol', 'Timeframe', 'Direction', 'Price', 'Signal Time']) {
    assert.equal(key in props, true, `${key} should be present`);
  }
});

test('a zero-valued number is kept, not treated as absent', () => {
  const payload = normalise({
    event: 'entry',
    symbol: 'OANDA:EURUSD',
    timeframe: '5',
    direction: 'long',
    price: '1.0925',
    quality: '0',
  });
  const props = toNotionProperties(payload);
  assert.deepEqual(props.Quality, { number: 0 });
});

test('column names can be remapped in one place', () => {
  const names = { ...PROPERTY_NAMES, price: 'Fill Price', time: 'Fired At' };
  const props = toNotionProperties(fullEntry(), names);
  assert.deepEqual(props['Fill Price'], { number: 4512.5 });
  assert.deepEqual(props['Fired At'], { date: { start: '2026-08-05T13:45:00.000Z' } });
  assert.equal('Price' in props, false);
  assert.equal('Signal Time' in props, false);
});

test('the documented name map and type map stay in step', () => {
  assert.deepEqual(Object.keys(PROPERTY_NAMES), Object.keys(PROPERTY_TYPES));
  assert.equal(PROPERTY_TYPES.name, 'title');
});

test('over-long note text is truncated to the Notion 2000-char limit', () => {
  const payload = normalise({
    event: 'entry',
    symbol: 'OANDA:XAUUSD',
    timeframe: '15',
    direction: 'long',
    price: 1,
    note: 'y'.repeat(4000),
  });
  const props = toNotionProperties(payload);
  assert.equal(props.Note.rich_text[0].text.content.length, 2000);
});

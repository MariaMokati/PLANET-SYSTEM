// =============================================================================
// PAYLOAD -> NOTION PAGE MAPPER
// -----------------------------------------------------------------------------
// Pure functions only, so the mapping can be unit-tested without a network or a
// Worker runtime. `index.js` does the HTTP; this file decides the shape.
//
// Optional properties are OMITTED ENTIRELY when the payload does not carry them.
// Notion rejects `null` for typed properties, so "absent" must mean "absent".
// =============================================================================

import { MAX_TEXT_LENGTH } from './payload.js';

/**
 * Notion column names, in one place.
 * Renamed a column in Notion? Change it here (or pass an override map to
 * `buildNotionPage`) — nothing else in the codebase hard-codes these strings.
 */
export const PROPERTY_NAMES = {
  name: 'Name',           // title
  event: 'Event',         // select
  symbol: 'Symbol',       // rich_text
  timeframe: 'Timeframe', // select
  direction: 'Direction', // select
  price: 'Price',         // number
  entry: 'Entry',         // number
  stop: 'Stop',           // number
  target: 'Target',       // number
  quality: 'Quality',     // number
  session: 'Session',     // select
  note: 'Note',           // rich_text
  time: 'Signal Time',    // date
};

/** Notion property types, keyed the same way. Documented in the README table. */
export const PROPERTY_TYPES = {
  name: 'title',
  event: 'select',
  symbol: 'rich_text',
  timeframe: 'select',
  direction: 'select',
  price: 'number',
  entry: 'number',
  stop: 'number',
  target: 'number',
  quality: 'number',
  session: 'select',
  note: 'rich_text',
  time: 'date',
};

const title = (text) => ({ title: [{ type: 'text', text: { content: String(text).slice(0, MAX_TEXT_LENGTH) } }] });
const richText = (text) => ({ rich_text: [{ type: 'text', text: { content: String(text).slice(0, MAX_TEXT_LENGTH) } }] });
const select = (name) => ({ select: { name: String(name).slice(0, 100) } });
const number = (n) => ({ number: n });
const date = (start) => ({ date: { start } });

/**
 * Compose the page title: "{SYMBOL} {DIRECTION} {EVENT}", e.g.
 * "OANDA:XAUUSD LONG ENTRY".
 */
export function buildTitle(payload) {
  return [payload.symbol, payload.direction, payload.event]
    .map((part) => String(part ?? '').toUpperCase())
    .join(' ')
    .trim();
}

/**
 * Map a validated payload to a Notion `properties` object.
 *
 * @param {object} payload  output of `validatePayload().value`
 * @param {object} [names]  override for `PROPERTY_NAMES`
 */
export function toNotionProperties(payload, names = PROPERTY_NAMES) {
  const props = {};

  props[names.name] = title(buildTitle(payload));
  props[names.event] = select(payload.event);
  props[names.symbol] = richText(payload.symbol);
  props[names.timeframe] = select(payload.timeframe);
  props[names.direction] = select(payload.direction);
  props[names.price] = number(payload.price);

  // Optional — omitted entirely when absent, never sent as null.
  for (const key of ['entry', 'stop', 'target', 'quality']) {
    if (typeof payload[key] === 'number') props[names[key]] = number(payload[key]);
  }
  if (payload.session) props[names.session] = select(payload.session);
  if (payload.note) props[names.note] = richText(payload.note);
  if (payload.time) props[names.time] = date(payload.time);

  return props;
}

/**
 * Full request body for `POST https://api.notion.com/v1/pages`.
 *
 * @param {object} payload     output of `validatePayload().value`
 * @param {string} databaseId  the target database id
 * @param {object} [names]     override for `PROPERTY_NAMES`
 */
export function buildNotionPage(payload, databaseId, names = PROPERTY_NAMES) {
  return {
    parent: { database_id: databaseId },
    properties: toNotionProperties(payload, names),
  };
}

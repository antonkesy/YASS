/** The languages the UI speaks. English is the source every other is translated from. */
export const LOCALES = ['en', 'de', 'fr', 'es'] as const;
export type Locale = (typeof LOCALES)[number];

/** A count-dependent text: `one` for exactly one in that language's sense, `other` for the rest. */
export type Plural = { one: string; other: string };

/** One message in every language. `{name}` in a text is filled from the call's params. */
export type Message = Record<Locale, string | Plural>;

/** One screen's or component group's messages, keyed by a short camelCase name. */
export type Area = Record<string, Message>;

export const LOCALES = ['en', 'de', 'fr', 'es'] as const;
export type Locale = (typeof LOCALES)[number];

export type Plural = { one: string; other: string };

export type Message = Record<Locale, string | Plural>;

export type Area = Record<string, Message>;

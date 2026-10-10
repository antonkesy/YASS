import { LOCALES, type Locale, type Message, type Plural } from './types';
import { messages } from './messages';

export { LOCALES, type Locale } from './types';

/** Each language by its own name: what the language menu lists. */
export const LOCALE_NAMES: Record<Locale, string> = {
	en: 'English',
	de: 'Deutsch',
	fr: 'Français',
	es: 'Español'
};

export const LOCALE_KEY = 'yass.locale';

type Messages = typeof messages;
/** `area.key`, for every message there is. */
export type MessageKey = {
	[A in keyof Messages]: `${A & string}.${keyof Messages[A] & string}`;
}[keyof Messages];

export type Params = Record<string, string | number>;

function isLocale(value: unknown): value is Locale {
	return typeof value === 'string' && (LOCALES as readonly string[]).includes(value);
}

/** This browser's choice if it made one, else the first of its languages the UI speaks. */
function initial(): Locale {
	try {
		const stored = localStorage.getItem(LOCALE_KEY);
		if (isLocale(stored)) return stored;
	} catch {
		// private mode or no storage: fall through to the browser's languages
	}
	if (typeof navigator !== 'undefined') {
		for (const tag of navigator.languages ?? [navigator.language]) {
			const base = tag?.slice(0, 2).toLowerCase();
			if (isLocale(base)) return base;
		}
	}
	return 'en';
}

let current = $state<Locale>(typeof window === 'undefined' ? 'en' : initial());

/**
 * The language the UI is in. Reading `i18n.locale` inside a component — or calling `t`,
 * which does — re-renders it when the language changes.
 */
export const i18n = {
	get locale(): Locale {
		return current;
	},
	set(locale: Locale) {
		current = locale;
		try {
			localStorage.setItem(LOCALE_KEY, locale);
		} catch {
			// keep it for this visit at least
		}
		if (typeof document !== 'undefined') document.documentElement.lang = locale;
	}
};

function lookup(key: MessageKey): Message {
	const [area, name] = key.split('.') as [keyof Messages, string];
	return (messages[area] as Record<string, Message>)[name];
}

/** One plural rule set per language, made the first time that language counts something. */
const plurals: Partial<Record<Locale, Intl.PluralRules>> = {};
const plural = (locale: Locale) => (plurals[locale] ??= new Intl.PluralRules(locale));

function fill(text: string, params?: Params): string {
	if (!params) return text;
	return text.replace(/\{(\w+)\}/g, (whole, name: string) =>
		name in params ? String(params[name]) : whole
	);
}

/**
 * A message in the current language, with `{name}` placeholders filled from `params`. A
 * plural message picks its form by `params.n`, the way the language counts.
 */
export function t(key: MessageKey, params?: Params): string {
	const text = lookup(key)[current];
	if (typeof text === 'string') return fill(text, params);
	const n = Number(params?.n ?? 0);
	const form = plural(current).select(n) === 'one' ? 'one' : 'other';
	return fill((text as Plural)[form], params);
}

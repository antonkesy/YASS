import { LOCALES, type Locale, type Message, type Plural } from './types';
import { messages } from './messages';

export { LOCALES, type Locale } from './types';

export const LOCALE_NAMES: Record<Locale, string> = {
	en: 'English',
	de: 'Deutsch',
	fr: 'Français',
	es: 'Español'
};

export const LOCALE_KEY = 'yass.locale';

type Messages = typeof messages;
export type MessageKey = {
	[A in keyof Messages]: `${A & string}.${keyof Messages[A] & string}`;
}[keyof Messages];

export type Params = Record<string, string | number>;

function isLocale(value: unknown): value is Locale {
	return typeof value === 'string' && (LOCALES as readonly string[]).includes(value);
}

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

/** Reading `i18n.locale` (or calling `t`) in a component re-renders it on language change. */
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

const plurals: Partial<Record<Locale, Intl.PluralRules>> = {};
const plural = (locale: Locale) => (plurals[locale] ??= new Intl.PluralRules(locale));

function fill(text: string, params?: Params): string {
	if (!params) return text;
	return text.replace(/\{(\w+)\}/g, (whole, name: string) =>
		name in params ? String(params[name]) : whole
	);
}

export function t(key: MessageKey, params?: Params): string {
	const text = lookup(key)[current];
	if (typeof text === 'string') return fill(text, params);
	const n = Number(params?.n ?? 0);
	const form = plural(current).select(n) === 'one' ? 'one' : 'other';
	return fill((text as Plural)[form], params);
}

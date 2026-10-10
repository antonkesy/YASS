import { t } from '$lib/i18n/index.svelte';

/**
 * SignalR prefixes every hub error. HubException text is meant for people, so it is unwrapped;
 * anything else is a bug and gets a generic message instead of its internals.
 */
const WRAPPED = /^An unexpected error occurred invoking '[^']*' on the server\. (\w+): (.*)$/s;

export function hubError(error: unknown): string {
	const raw = error instanceof Error ? error.message : String(error);
	const match = WRAPPED.exec(raw);
	if (!match) return raw;

	const [, type, message] = match;
	return type === 'HubException' ? message : t('dialogs.serverError');
}

import { t } from '$lib/i18n/index.svelte';

/**
 * SignalR wraps every hub error as
 * `An unexpected error occurred invoking 'Method' on the server. HubException: <message>`,
 * whether or not detailed errors are enabled. The hub raises HubException specifically for
 * text meant to be read by a person, so that prefix is noise in front of the useful part.
 *
 * Anything that is not a HubException stays as the generic sentence: those are bugs, and
 * their internals are not something to put in front of a user.
 */
const WRAPPED = /^An unexpected error occurred invoking '[^']*' on the server\. (\w+): (.*)$/s;

export function hubError(error: unknown): string {
	const raw = error instanceof Error ? error.message : String(error);
	const match = WRAPPED.exec(raw);
	if (!match) return raw;

	const [, type, message] = match;
	return type === 'HubException' ? message : t('dialogs.serverError');
}

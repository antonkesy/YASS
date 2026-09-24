import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { browser } from '$app/environment';
import { base } from '$app/paths';
import { createHubClient, type YawssHubClient } from './client';

export * from './client';
export * from './state';

let instance: YawssHubClient | null = null;

/** The app-wide hub client. Created on first use in the browser; `start()` is safe to call repeatedly. */
export function getHub(): YawssHubClient {
	if (!instance) {
		if (!browser) throw new Error('The hub is only available in the browser.');
		const connection = new HubConnectionBuilder()
			.withUrl(`${base}/hub`)
			.withHubProtocol(new MessagePackHubProtocol())
			.withAutomaticReconnect([0, 500, 1000, 2000, 5000, 10_000, 10_000, 10_000])
			.configureLogging(LogLevel.Warning)
			.build();
		instance = createHubClient(connection);
	}
	return instance;
}

/** URL of a page image; the hash makes it immutable, so the browser caches it forever. */
export function imageUrl(
	sessionId: string,
	index: number,
	hash: string,
	kind: 'thumb' | 'preview'
) {
	return `${base}/img/${encodeURIComponent(sessionId)}/${index}/${kind}?h=${hash}`;
}

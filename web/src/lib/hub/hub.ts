import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { browser } from '$app/environment';
import { base } from '$app/paths';
import { createBridgeConnection, desktopExternal } from './bridge';
import { createHubClient, type YassHubClient } from './client';

export * from './client';
export * from './state';

let instance: YassHubClient | null = null;

/**
 * The app-wide hub client. Created on first use in the browser; `start()` is safe to call
 * repeatedly. Inside the desktop app it talks over the webview's message bridge; everywhere
 * else over SignalR. Either way the rest of the app sees the same client.
 */
export function getHub(): YassHubClient {
	if (!instance) {
		if (!browser) throw new Error('The hub is only available in the browser.');
		const external = desktopExternal();
		if (external) {
			instance = createHubClient(createBridgeConnection(external));
			return instance;
		}
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
	kind: 'thumb' | 'preview' | 'frame'
) {
	return `${base}/img/${encodeURIComponent(sessionId)}/${index}/${kind}?h=${hash}`;
}

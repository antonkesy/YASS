import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { browser } from '$app/environment';
import { base } from '$app/paths';
import {
	createBridgeConnection,
	createDesktopHost,
	desktopExternal,
	type DesktopHost
} from './bridge';
import { createHubClient, type YassHubClient } from './client';

export * from './client';
export * from './state';

let instance: YassHubClient | null = null;
let desktop: DesktopHost | null = null;

export function getHub(): YassHubClient {
	if (!instance) {
		if (!browser) throw new Error('The hub is only available in the browser.');
		const external = desktopExternal();
		if (external) {
			const connection = createBridgeConnection(external);
			desktop = createDesktopHost(connection);
			instance = createHubClient(connection);
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

export function getDesktop(): DesktopHost | null {
	getHub();
	return desktop;
}

export function imageUrl(
	sessionId: string,
	index: number,
	hash: string,
	kind: 'thumb' | 'preview' | 'frame'
) {
	return `${base}/img/${encodeURIComponent(sessionId)}/${index}/${kind}?h=${hash}`;
}

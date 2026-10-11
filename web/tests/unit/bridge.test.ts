import { describe, expect, it } from 'vitest';
import { get } from 'svelte/store';
import { HubConnectionState } from '@microsoft/signalr';
import {
	createBridgeConnection,
	createDesktopHost,
	desktopExternal,
	type DesktopExternal
} from '$lib/hub/bridge';
import { createHubClient } from '$lib/hub/client';
import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
import { sessionOf } from './fixtures';

function fakeExternal() {
	const sent: { id: number; method: string; args: unknown[] }[] = [];
	let receive: ((message: string) => void) | null = null;
	const external: DesktopExternal & {
		sent: typeof sent;
		push(message: unknown): void;
	} = {
		sent,
		sendMessage: (message) => sent.push(JSON.parse(message)),
		receiveMessage: (callback) => (receive = callback),
		push: (message) => receive?.(typeof message === 'string' ? message : JSON.stringify(message))
	};
	return external;
}

describe('createBridgeConnection', () => {
	it('sends calls as {id, method, args} and resolves with the matching result', async () => {
		const external = fakeExternal();
		const connection = createBridgeConnection(external);

		const first = connection.invoke('GetSession', 'abc');
		const second = connection.invoke('ListSessions');
		expect(external.sent).toEqual([
			{ id: 1, method: 'GetSession', args: ['abc'] },
			{ id: 2, method: 'ListSessions', args: [] }
		]);

		external.push({ id: 2, result: [] });
		external.push({ id: 1, result: { Id: 'abc' } });
		await expect(first).resolves.toEqual({ Id: 'abc' });
		await expect(second).resolves.toEqual([]);
	});

	it('rejects with the host error message', async () => {
		const external = fakeExternal();
		const call = createBridgeConnection(external).invoke('GetSession', 'nope');
		external.push({ id: 1, error: 'Session nope not found.' });
		await expect(call).rejects.toThrow('Session nope not found.');
	});

	it('sends undefined arguments as null, as the host binds them to defaults', () => {
		const external = fakeExternal();
		void createBridgeConnection(external).invoke('StartScan', undefined);
		expect(external.sent[0].args).toEqual([null]);
	});

	it('dispatches events to their handlers and ignores junk', () => {
		const external = fakeExternal();
		const connection = createBridgeConnection(external);
		const seen: unknown[][] = [];
		const handler = (...args: unknown[]) => seen.push(args);
		connection.on('PageAdded', handler);

		external.push({ event: 'PageAdded', args: ['s1', { Index: 0 }] });
		external.push('not json');
		external.push({ id: 99, result: 'nobody asked' });
		connection.off('PageAdded', handler);
		external.push({ event: 'PageAdded', args: ['s1', { Index: 1 }] });

		expect(seen).toEqual([['s1', { Index: 0 }]]);
	});

	it('is connected once started', async () => {
		const connection = createBridgeConnection(fakeExternal());
		expect(connection.state).toBe(HubConnectionState.Disconnected);
		await connection.start();
		expect(connection.state).toBe(HubConnectionState.Connected);
		await connection.stop();
		expect(connection.state).toBe(HubConnectionState.Disconnected);
	});

	it('drives the hub client end to end', async () => {
		const external = fakeExternal();
		const client = createHubClient(createBridgeConnection(external));
		const started = client.start();

		await Promise.resolve();
		for (const call of external.sent) {
			if (call.method === 'GetScannerStatus')
				external.push({ id: call.id, result: { State: ScannerState.Ready, CheckedAt: '' } });
			if (call.method === 'ListSessions')
				external.push({ id: call.id, result: [sessionOf('s1', 1)] });
		}
		await started;

		expect(get(client.connection)).toBe('connected');
		expect(get(client.scanner)?.State).toBe(ScannerState.Ready);
		expect([...get(client.sessions).keys()]).toEqual(['s1']);

		external.push({ event: 'ExportProgress', args: ['s1', 40] });
		await Promise.resolve();
		expect(get(client.exportProgress)).toEqual({ s1: 40 });
	});
});

describe('desktopExternal', () => {
	it('is null in a browser and the channel inside the desktop app', () => {
		expect(desktopExternal()).toBeNull();
		const external = fakeExternal();
		Object.defineProperty(window, 'external', { value: external, configurable: true });
		try {
			expect(desktopExternal()).toBe(external);
		} finally {
			Object.defineProperty(window, 'external', { value: undefined, configurable: true });
		}
	});
});

describe('createDesktopHost', () => {
	it('calls the desktop-only methods by name', async () => {
		const external = fakeExternal();
		const host = createDesktopHost(createBridgeConnection(external));

		const picked = host.pickSavePath('scan', '.pdf');
		const folder = host.pickFolder();
		const saved = host.saveExport('/export/s1/t.pdf', '/home/me/scan.pdf', true);
		void host.openUrl('https://example.org/');
		expect(external.sent).toEqual([
			{ id: 1, method: 'PickSavePath', args: ['scan', '.pdf'] },
			{ id: 2, method: 'PickFolder', args: [] },
			{ id: 3, method: 'SaveExport', args: ['/export/s1/t.pdf', '/home/me/scan.pdf', true] },
			{ id: 4, method: 'OpenUrl', args: ['https://example.org/'] }
		]);

		external.push({ id: 1, result: null });
		external.push({ id: 2, result: '/home/me' });
		external.push({ id: 3, result: '/home/me/scan.pdf' });
		await expect(picked).resolves.toBeNull();
		await expect(folder).resolves.toBe('/home/me');
		await expect(saved).resolves.toBe('/home/me/scan.pdf');
	});
});

import { describe, expect, it, vi } from 'vitest';
import { get } from 'svelte/store';
import { HubConnectionState } from '@microsoft/signalr';
import { createHubClient, type ConnectionLike } from '$lib/hub/client';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { sessionOf } from './fixtures';

type Handler = (...args: unknown[]) => void;

/** In-memory stand-in for HubConnection: records invocations and lets tests push server events. */
function fakeConnection(sessions: SessionDto[] = []) {
	const handlers = new Map<string, Handler[]>();
	let onreconnecting: (() => void) | null = null;
	let onreconnected: (() => void) | null = null;
	let onclose: (() => void) | null = null;
	const invocations: [string, unknown[]][] = [];
	const fake = {
		state: HubConnectionState.Disconnected,
		startFailures: 0,
		start: vi.fn(async () => {
			if (fake.startFailures > 0) {
				fake.startFailures--;
				throw new Error('boom');
			}
			fake.state = HubConnectionState.Connected;
		}),
		stop: vi.fn(async () => {
			fake.state = HubConnectionState.Disconnected;
		}),
		on: (name: string, h: Handler) => handlers.set(name, [...(handlers.get(name) ?? []), h]),
		off: (name: string, h: Handler) =>
			handlers.set(
				name,
				(handlers.get(name) ?? []).filter((x) => x !== h)
			),
		invoke: vi.fn(async (name: string, ...args: unknown[]) => {
			invocations.push([name, args]);
			switch (name) {
				case 'GetScannerStatus':
					return { State: ScannerState.Ready, CheckedAt: '' };
				case 'ListSessions':
					return sessions;
				case 'GetSession':
					return sessions.find((s) => s.Id === args[0]) ?? Promise.reject(new Error('not found'));
				default:
					return undefined;
			}
		}),
		onreconnecting: (h: () => void) => (onreconnecting = h),
		onreconnected: (h: () => void) => (onreconnected = h),
		onclose: (h: () => void) => (onclose = h),
		emit: (name: string, ...args: unknown[]) =>
			Promise.all((handlers.get(name) ?? []).map((h) => h(...args))),
		reconnecting: () => onreconnecting?.(),
		reconnected: () => onreconnected?.(),
		close: () => onclose?.(),
		invocations
	};
	return fake;
}

const session = (Id: string, pages = 0): SessionDto => sessionOf(Id, pages);

describe('hub client', () => {
	it('loads status and sessions on start', async () => {
		const conn = fakeConnection([session('a', 2)]);
		const client = createHubClient(conn as unknown as ConnectionLike);
		expect(get(client.connection)).toBe('disconnected');
		await client.start();
		expect(get(client.connection)).toBe('connected');
		expect(get(client.scanner)?.State).toBe(ScannerState.Ready);
		expect(get(client.sessions).get('a')?.Pages).toHaveLength(2);
	});

	it('applies server pushes to the stores', async () => {
		const conn = fakeConnection([session('a', 1)]);
		const client = createHubClient(conn as unknown as ConnectionLike);
		await client.start();
		await conn.emit('PageAdded', 'a', {
			Index: 1,
			Rotation: 0,
			Deleted: false,
			IsBlank: true,
			Hash: 'x',
			ThumbnailReady: false
		});
		expect(get(client.sessions).get('a')?.Pages).toHaveLength(2);
		await conn.emit('PageUpdated', 'a', {
			Index: 1,
			Rotation: 0,
			Deleted: true,
			IsBlank: true,
			Hash: 'x',
			ThumbnailReady: true
		});
		expect(get(client.sessions).get('a')?.Pages[1].ThumbnailReady).toBe(true);
		await conn.emit('SessionChanged', {
			...session('a'),
			State: SessionState.Done,
			Error: 'deleted'
		});
		expect(get(client.sessions).has('a')).toBe(false);
		await conn.emit('ExportProgress', 'a', 40);
		expect(get(client.exportProgress)).toEqual({ a: 40 });
		await conn.emit('ScannerStatusChanged', { State: ScannerState.Busy, CheckedAt: '' });
		expect(get(client.scanner)?.State).toBe(ScannerState.Busy);
	});

	it('forgets the progress of an export once the session is no longer exporting', async () => {
		const conn = fakeConnection([session('a', 1)]);
		const client = createHubClient(conn as unknown as ConnectionLike);
		await client.start();
		await conn.emit('SessionChanged', { ...session('a', 1), State: SessionState.Exporting });
		await conn.emit('ExportProgress', 'a', 100);
		expect(get(client.exportProgress)).toEqual({ a: 100 });
		await conn.emit('SessionChanged', { ...session('a', 1), State: SessionState.Done });
		expect(get(client.exportProgress)).toEqual({});
	});

	it('fetches an unknown session when its first page arrives', async () => {
		const conn = fakeConnection([]);
		const client = createHubClient(conn as unknown as ConnectionLike);
		await client.start();
		conn.invocations.length = 0;
		// Simulate the server knowing the session even though ListSessions ran before it existed.
		conn.invoke.mockImplementationOnce(async () => session('new', 0));
		await conn.emit('PageAdded', 'new', {
			Index: 0,
			Rotation: 0,
			Deleted: false,
			IsBlank: false,
			Hash: 'h',
			ThumbnailReady: false
		});
		expect(conn.invoke).toHaveBeenCalledWith('GetSession', 'new');
		expect(get(client.sessions).get('new')?.Pages).toHaveLength(1);
	});

	it('reports reconnecting and reloads after reconnect', async () => {
		const conn = fakeConnection([]);
		const client = createHubClient(conn as unknown as ConnectionLike);
		await client.start();
		conn.reconnecting();
		expect(get(client.connection)).toBe('reconnecting');
		conn.invocations.length = 0;
		conn.reconnected();
		await vi.waitFor(() => expect(get(client.connection)).toBe('connected'));
		await vi.waitFor(() => expect(conn.invocations.map(([n]) => n)).toContain('ListSessions'));
	});

	it('retries start until it succeeds', async () => {
		vi.useFakeTimers();
		try {
			const conn = fakeConnection([]);
			conn.startFailures = 2;
			const client = createHubClient(conn as unknown as ConnectionLike);
			const started = client.start();
			await vi.advanceTimersByTimeAsync(500);
			await vi.advanceTimersByTimeAsync(1000);
			await started;
			expect(conn.start).toHaveBeenCalledTimes(3);
			expect(get(client.connection)).toBe('connected');
		} finally {
			vi.useRealTimers();
		}
	});

	it('updateLocal changes a session without a round trip and stop closes the connection', async () => {
		const conn = fakeConnection([session('a', 1)]);
		const client = createHubClient(conn as unknown as ConnectionLike);
		await client.start();
		client.updateLocal('a', (s) => ({ ...s, State: SessionState.Exporting }));
		expect(get(client.sessions).get('a')?.State).toBe(SessionState.Exporting);
		await client.stop();
		expect(conn.stop).toHaveBeenCalled();
		expect(get(client.connection)).toBe('disconnected');
	});
});

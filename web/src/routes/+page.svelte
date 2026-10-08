<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { hubError } from '$lib/hub/errors';
	import { toast } from '$lib/toast';
	import { cameFrom } from '$lib/navigation';
	import { getHub, sortedSessions } from '$lib/hub/hub';
	import { devicesStale } from '$lib/hub/state';
	import type {
		ScannerDeviceDto,
		ScannerStatusDto,
		ScanOptionsDto
	} from '$lib/hub/generated/Yass.Core.Hub';
	import ScanStage from '$lib/components/ScanStage.svelte';
	import PastScansPanel from '$lib/components/PastScansPanel.svelte';
	import { settings } from '$lib/settings';
	import { saveToBrowser } from '$lib/review/download';
	import { documentMetadata, exportPrefs, historyPrefs, scannerPrefs } from '$lib/presets';
	import { pickable, remember, scannerName } from '$lib/scanners';
	import { screenTransition } from '$lib/motion';
	import { expiredSessions } from '$lib/sessions/retention';
	import { untrack } from 'svelte';
	import { SvelteSet } from 'svelte/reactivity';

	const client = getHub();
	const scanner = client.scanner;
	const connection = client.connection;
	const sessions = client.sessions;

	let starting = $state(false);
	let working = $state(false);
	let error = $state<string | null>(null);
	let effective = $state<ScanOptionsDto | null>(null);
	/** Scanners the server can see: the stage's picker, and the name a chosen one goes by. */
	let devices = $state<ScannerDeviceDto[]>([]);

	// The stage shows what the next scan will use, so the options are loaded up front
	// rather than only at the moment Scan is pressed.
	$effect(() => {
		if ($connection === 'connected' && !effective) {
			client.hub
				.getEffectiveOptions()
				.then((o) => (effective = o))
				.catch(() => undefined);
		}
	});

	/**
	 * The scanner list is asked for on every (re)connect and again whenever the scanner status
	 * changes: a scanner plugged in after the page loaded, or one the driver was still
	 * enumerating the first time, shows up in the picker without a reload. Quietly: the stage
	 * is worth showing whether or not the driver felt like answering.
	 */
	let seenStatus: ScannerStatusDto | null = null;
	let lastConnected = false;
	$effect(() => {
		const connected = $connection === 'connected';
		const status = $scanner;
		const reconnected = connected && !lastConnected;
		const changed = devicesStale(seenStatus, status);
		lastConnected = connected;
		seenStatus = status;
		if (!connected || !(reconnected || changed)) return;
		client.hub
			.listDevices()
			.then((d) => {
				devices = d;
				// Remembered, so Settings can still list a scanner once it is unplugged.
				scannerPrefs.set(remember($scannerPrefs, d));
			})
			.catch(() => undefined);
	});

	const recent = $derived(sortedSessions($sessions));

	// Taken once on mount: coming back from a session slides in from the left and
	// highlights that scan; a cold load just fades.
	const landed = cameFrom.take();
	const cameBack = landed !== null;
	// A view transition already slides the screen in; its own entrance would be a second slide.
	const entrance = screenTransition.active
		? ''
		: cameBack
			? 'animate-slide-from-left'
			: 'animate-fade-in';

	/**
	 * What the next scan will actually use: the server's defaults with this browser's
	 * overrides applied. The stage shows this rather than the bare defaults, which would
	 * advertise settings the scan is not going to run with.
	 */
	const planned = $derived(effective ? { ...effective, ...$settings } : null);

	/** The picker's scanners: this browser's names for them, and none it has hidden. */
	const offered = $derived(pickable($scannerPrefs, devices, planned?.Device));

	/**
	 * The chosen scanner as a person reads it. Nothing when none is chosen — the review header's
	 * status pill names whatever the server would pick — and the bare id for one the server has
	 * stopped listing, which at least says something is wrong.
	 */
	const deviceName = $derived(
		planned?.Device
			? scannerName(
					$scannerPrefs,
					planned.Device,
					devices.find((d) => d.Id === planned.Device)?.Name
				)
			: null
	);

	async function scan() {
		starting = true;
		error = null;
		try {
			const session = await client.hub.startScan($settings);
			await goto(resolve('/session/[id]', { id: session.Id }));
		} catch (e) {
			error = hubError(e);
		} finally {
			starting = false;
		}
	}

	/**
	 * One download per scan. The browser asks before saving several files at once,
	 * which is the right prompt to get: this really is several separate saves.
	 */
	async function downloadAll(ids: string[]) {
		error = null;
		working = true;
		try {
			let files = 0;
			for (const id of ids) {
				files += saveToBrowser(
					await client.hub.export(id, {
						Format: $exportPrefs.format,
						Metadata: documentMetadata($exportPrefs),
						ToServer: false
					})
				);
			}
			toast.show(files === 1 ? 'Downloaded' : `Downloaded ${files} files`);
		} catch (e) {
			error = hubError(e);
		} finally {
			working = false;
		}
	}

	/**
	 * The auto-clear preference, carried out: whichever browser has it set deletes the scans
	 * past its timeframe whenever the list changes and once a minute besides, so a scan ages
	 * out while the page is simply left open. One delete per scan at a time; a failed one
	 * is tried again on the next look rather than reported, there being nothing to do about it.
	 */
	const sweeping = new SvelteSet<string>();

	async function sweep() {
		if ($connection !== 'connected') return;
		for (const s of expiredSessions(sortedSessions($sessions), $historyPrefs)) {
			if (sweeping.has(s.Id)) continue;
			sweeping.add(s.Id);
			try {
				await client.hub.deleteSession(s.Id);
			} catch {
				// gone already, or the server said no: the next sweep will see which
			} finally {
				sweeping.delete(s.Id);
			}
		}
	}

	$effect(() => {
		void $sessions;
		void $historyPrefs;
		void $connection;
		void untrack(sweep);
	});

	$effect(() => {
		const timer = setInterval(() => void sweep(), 60_000);
		return () => clearInterval(timer);
	});

	async function deleteAll(ids: string[]) {
		error = null;
		working = true;
		try {
			for (const id of ids) await client.hub.deleteSession(id);
			toast.show(`${ids.length} scan${ids.length === 1 ? '' : 's'} deleted`);
		} catch (e) {
			error = hubError(e);
		} finally {
			working = false;
		}
	}

	async function clearAll() {
		error = null;
		try {
			const gone = await client.hub.clearSessions();
			toast.show(`${gone} scan${gone === 1 ? '' : 's'} deleted`);
		} catch (e) {
			error = hubError(e);
		}
	}

	function openSettings() {
		void goto(resolve('/settings'));
	}

	/** Straight to naming a new preset, which is what Save as preset promised. */
	function savePreset() {
		void goto(resolve('/settings'), { state: { newPreset: true } });
	}
</script>

<svelte:head><title>YASS</title></svelte:head>

<main class="mx-auto flex max-w-[2200px] flex-col gap-6 px-4 pt-3 pb-7 sm:px-7 {entrance}">
	{#if error}
		<p class="rounded-[14px] bg-danger-bg px-4 py-2 font-semibold text-danger" role="alert">
			{error}
		</p>
	{/if}

	<!--
		Side by side, the panes get exactly the height left over after the header and this
		main's own pt-3 + pb-7 (2.5rem) -- so the home screen fits without scrolling
		and each pane scrolls its own overflow instead. Stacked (below lg) they keep their
		natural height and the page scrolls, which is the right behaviour on a phone.
	-->
	<div
		class="flex flex-wrap items-stretch gap-6 lg:h-[calc(100dvh-var(--app-header-h)-var(--app-footer-h)-2.5rem)] lg:min-h-0 lg:flex-nowrap"
	>
		<ScanStage
			status={$scanner}
			connection={$connection}
			options={planned}
			devices={offered}
			{deviceName}
			busy={starting}
			onscan={scan}
			onsettings={openSettings}
			onsavepreset={savePreset}
		/>
		<PastScansPanel
			sessions={recent}
			busy={working}
			onclear={clearAll}
			ondownload={downloadAll}
			ondelete={deleteAll}
			{landed}
		/>
	</div>
</main>

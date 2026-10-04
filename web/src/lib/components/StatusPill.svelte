<script lang="ts">
	import type { ScannerStatusDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { formatTime, scannerStateLabel } from '$lib/format';

	let {
		status,
		connection,
		pages = null
	}: {
		status: ScannerStatusDto | null;
		connection: ConnectionState;
		/** Pages scanned so far, when a scan is running. Shown as "Scanning · N pages". */
		pages?: number | null;
	} = $props();

	let open = $state(false);

	const offline = $derived(connection !== 'connected');
	const scanning = $derived(!offline && status?.State === ScannerState.Busy);

	const label = $derived(
		offline
			? connection === 'connecting'
				? 'Connecting…'
				: 'Offline'
			: scanning && pages
				? `Scanning · ${pages} ${pages === 1 ? 'page' : 'pages'}`
				: scannerStateLabel(status?.State)
	);

	// The mockup pills are always white; only the dot carries the state.
	const dot = $derived(
		offline || status?.State === ScannerState.Unknown
			? 'bg-ink-faint'
			: status?.State === ScannerState.Ready
				? 'bg-success animate-pulse-ready'
				: scanning
					? 'bg-accent animate-pulse-busy'
					: 'bg-danger'
	);

	const connectionLabel = $derived(
		connection === 'connected'
			? 'Connected'
			: connection === 'connecting'
				? 'Connecting…'
				: connection === 'reconnecting'
					? 'Reconnecting…'
					: 'Offline'
	);

	/**
	 * What the panel lists. Rows the server has nothing for are left out rather than
	 * shown with a dash: a missing device name is already said by the state.
	 */
	const rows = $derived(
		[
			['Server', connectionLabel],
			['Scanner', status?.DeviceName ?? null],
			['State', offline ? null : scannerStateLabel(status?.State)],
			['Pages so far', scanning && pages !== null ? String(pages) : null],
			['Details', status?.Message ?? null],
			['Last checked', status?.CheckedAt ? formatTime(status.CheckedAt) : null]
		].filter((row): row is [string, string] => row[1] !== null && row[1] !== '')
	);
</script>

<div class="relative flex justify-center">
	<!-- The pill stays a status for screen readers; the click is on top of that, not instead. -->
	<button
		type="button"
		class="inline-flex h-[34px] items-center gap-2 rounded-full bg-surface py-0 pr-[14px] pl-3 text-[13px] font-semibold text-ink shadow-chip transition-colors hover:bg-accent-wash"
		aria-expanded={open}
		aria-haspopup="dialog"
		aria-label="Scanner details"
		onclick={() => (open = !open)}
		data-testid="status-pill"
	>
		<span role="status" aria-live="polite" class="contents">
			<span class="h-2 w-2 flex-none rounded-full {dot}" aria-hidden="true"></span>
			{label}
			{#if !offline && !scanning && status?.DeviceName}
				<span class="hidden font-medium text-ink-muted sm:inline">· {status.DeviceName}</span>
			{/if}
		</span>
	</button>

	{#if open}
		<!-- Click-catcher so the panel closes on any outside press, including a tap. -->
		<button
			type="button"
			class="fixed inset-0 z-30 cursor-default"
			aria-label="Close scanner details"
			onclick={() => (open = false)}
		></button>
		<div
			class="absolute top-[calc(100%+8px)] left-1/2 z-31 w-max max-w-[min(320px,calc(100vw-32px))] -translate-x-1/2 animate-pop-in rounded-[16px] bg-surface p-4 text-left shadow-menu"
			role="dialog"
			aria-label="Scanner details"
			data-testid="scanner-details"
		>
			<p class="mb-2 text-[11px] font-bold tracking-[0.06em] text-ink-faint uppercase">Scanner</p>
			<dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-[13px]">
				{#each rows as [name, value] (name)}
					<dt class="font-semibold text-ink-muted">{name}</dt>
					<dd class="min-w-0 font-medium break-words text-ink">{value}</dd>
				{/each}
			</dl>
		</div>
	{/if}
</div>

<script lang="ts">
	import type { ScannerStatusDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { scannerStateLabel } from '$lib/format';
	import ScannerDetails from './ScannerDetails.svelte';

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

	/** Hovering the pill shows the panel until the pointer leaves; a press pins it until a press outside. */
	let open = $state<'hover' | 'pinned' | null>(null);

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
</script>

<div
	class="relative flex justify-center"
	role="presentation"
	onmouseenter={() => {
		if (open === null) open = 'hover';
	}}
	onmouseleave={() => {
		if (open === 'hover') open = null;
	}}
>
	<!-- The pill stays a status for screen readers; the click is on top of that, not instead. -->
	<button
		type="button"
		class="inline-flex h-[34px] items-center gap-2 rounded-full bg-surface py-0 pr-[14px] pl-3 text-[13px] font-semibold text-ink shadow-chip transition-colors hover:bg-accent-wash"
		aria-expanded={open === 'pinned'}
		aria-haspopup="dialog"
		aria-label="Scanner details"
		onclick={() => (open = open === 'pinned' ? null : 'pinned')}
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

	{#if open !== null}
		<ScannerDetails
			{status}
			{connection}
			{pages}
			pinned={open === 'pinned'}
			onclose={() => (open = null)}
		/>
	{/if}
</div>

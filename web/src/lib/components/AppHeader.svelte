<script lang="ts">
	import { base, resolve } from '$app/paths';
	import { page as route } from '$app/state';
	import { getHub, sortedSessions } from '$lib/hub/hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import AppMenu from './AppMenu.svelte';
	import { Icon } from '$lib/icons';
	import { cameFrom } from '$lib/navigation';

	const client = getHub();
	const scanner = client.scanner;
	const connection = client.connection;
	const sessions = client.sessions;
	const exportProgress = client.exportProgress;

	// Home wears the brand; every other route wears a way back to it.
	const atHome = $derived(route.url.pathname === resolve('/'));

	const scanning = $derived(
		sortedSessions($sessions).find((s) => s.State === SessionState.Scanning) ?? null
	);
	const exporting = $derived(
		Object.entries($exportProgress).find(([id, percent]) => {
			const s = $sessions.get(id);
			return s?.State === SessionState.Exporting && percent < 100;
		})?.[1] ?? null
	);
</script>

<header
	class="sticky top-0 z-20 flex h-16 items-center gap-2.5 bg-header px-4 backdrop-blur-[14px] sm:px-7"
>
	{#if atHome}
		<img src="{base}/icons/icon.svg" alt="" class="h-8 w-8 rounded-[9px]" />
		<span class="text-[19px] font-extrabold tracking-[-0.01em]">YASS</span>
	{:else}
		<!-- Label and arrow are one link: the words are the obvious thing to click. -->
		<a
			href={resolve('/')}
			onclick={() => cameFrom.leaving(route.params.id ?? '')}
			class="group flex items-center gap-2.5 rounded-[12px] text-[15px] font-bold text-ink-3 transition-colors hover:text-ink"
		>
			<span
				class="flex h-10 w-10 items-center justify-center rounded-[12px] bg-surface text-ink shadow-chip transition-colors group-hover:bg-accent-wash"
			>
				<Icon name="back" />
			</span>
			Back to home
		</a>
	{/if}

	<div class="flex-1"></div>

	<AppMenu status={$scanner} connection={$connection} pages={scanning?.Pages.length ?? null} />

	<!--
		Sub-page scan progress is not something the scanner reports, so the rail runs
		indeterminate while pages are arriving and switches to real percentages once an
		export is under way, which the hub does report.
	-->
	{#if scanning || exporting !== null}
		<div class="pointer-events-none absolute inset-x-0 bottom-0 h-[3px] overflow-hidden">
			{#if exporting !== null}
				<div
					class="h-full rounded-r-[3px] bg-linear-to-r from-accent-sky to-accent transition-[width] duration-100 ease-linear"
					style="width: {exporting}%"
				></div>
			{:else}
				<div class="h-full w-1/4 animate-rail bg-linear-to-r from-accent-sky to-accent"></div>
			{/if}
		</div>
	{/if}
</header>

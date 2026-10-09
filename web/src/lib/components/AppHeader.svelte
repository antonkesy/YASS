<script lang="ts">
	import { resolve } from '$app/paths';
	import { page as route } from '$app/state';
	import { getHub, sortedSessions } from '$lib/hub/hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import StatusPill from './StatusPill.svelte';
	import { Icon } from '$lib/icons';
	import { cameFrom } from '$lib/navigation';

	const client = getHub();
	const scanner = client.scanner;
	const connection = client.connection;
	const sessions = client.sessions;
	const exportProgress = client.exportProgress;

	// Home wears the name in the middle; every other route wears a way back to it.
	const atHome = $derived(route.url.pathname === resolve('/'));
	// Review is where the scanner's state matters mid-task, so it sits in the middle there.
	const atReview = $derived(route.params.id !== undefined);

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

<!--
	Three columns rather than a flex row with a spacer, so whatever sits in the middle is
	centred on the viewport and not on the space left between the two ends.
-->
<header
	class="sticky top-0 z-20 grid h-16 grid-cols-[1fr_auto_1fr] items-center gap-2.5 bg-header px-4 backdrop-blur-[14px] sm:px-7"
>
	<div class="flex min-w-0 items-center gap-2.5">
		{#if !atHome}
			<!-- Label and arrow are one link: the words are the obvious thing to click. -->
			<a
				href={resolve('/')}
				onclick={() => cameFrom.leaving(route.params.id ?? '')}
				aria-label="Back to home"
				class="group flex items-center gap-2.5 rounded-[12px] text-[15px] font-bold text-ink-3 transition-colors hover:text-ink"
			>
				<span
					class="flex h-10 w-10 flex-none items-center justify-center rounded-[12px] bg-surface text-ink shadow-chip transition-colors group-hover:bg-accent-wash"
				>
					<Icon name="back" />
				</span>
				<span class="hidden sm:inline">Back to home</span>
			</a>
		{/if}
	</div>

	<!--
		On review, a wide screen puts the page between a narrow strip and a wider sidebar, so
		its centre sits left of the window's: the pill moves by half the difference to stay
		over the page rather than over the gap beside it.
	-->
	<div
		class="flex min-w-0 items-center justify-center {atReview
			? 'lg:-translate-x-[calc((var(--review-aside-w)-var(--review-strip-w))/2)]'
			: ''}"
		data-testid="header-centre"
	>
		{#if atHome}
			<!-- The name is the whole of the brand here: no logo, no short form at the left. -->
			<span
				class="truncate text-[17px] font-extrabold tracking-[-0.01em] text-ink sm:text-[20px]"
				data-testid="app-name"
			>
				Yet Another Scanner Slop
			</span>
		{:else if atReview}
			<StatusPill
				status={$scanner}
				connection={$connection}
				pages={scanning?.Pages.length ?? null}
			/>
		{/if}
	</div>

	<!--
		Empty, and kept: the third column balances the first, so the middle stays centred on
		the window. GitHub lives in the footer.
	-->
	<div></div>

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

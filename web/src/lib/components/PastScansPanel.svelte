<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import SessionList from './SessionList.svelte';

	let { sessions }: { sessions: SessionDto[] } = $props();

	const pages = $derived(sessions.reduce((n, s) => n + s.Pages.length, 0));
</script>

<section
	class="flex animate-fade-in flex-col gap-4 rounded-[28px] bg-surface p-[22px] shadow-card"
	style:flex="1.25 1 420px"
	data-testid="past-scans"
>
	<div class="flex items-baseline gap-2">
		<h2 class="text-[19px] font-extrabold tracking-[-0.01em]">Past scans</h2>
		<span class="text-[13px] text-ink-muted">
			{sessions.length}
			{sessions.length === 1 ? 'scan' : 'scans'}
		</span>
	</div>

	<SessionList {sessions} />

	{#if sessions.length > 0}
		<p class="mt-auto border-t border-line pt-3 text-xs text-ink-muted">
			{sessions.length}
			{sessions.length === 1 ? 'scan' : 'scans'} · {pages}
			{pages === 1 ? 'page' : 'pages'} on server
		</p>
	{/if}
</section>

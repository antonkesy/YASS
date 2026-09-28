<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { imageUrl } from '$lib/hub/hub';
	import { resolve } from '$app/paths';
	import {
		activePageCount,
		formatBytes,
		formatTime,
		lastEventPath,
		sessionStatus,
		sessionTitle
	} from '$lib/format';
	import SessionStatusPill from './SessionStatusPill.svelte';

	let { session }: { session: SessionDto } = $props();

	const first = $derived(session.Pages.find((p) => !p.Deleted && p.ThumbnailReady) ?? null);
	const pages = $derived(activePageCount(session));
	const path = $derived(lastEventPath(session));
	const status = $derived(sessionStatus(session));

	const meta = $derived(
		[
			formatTime(session.CreatedAt),
			`${pages} ${pages === 1 ? 'page' : 'pages'}`,
			formatBytes(session.SizeBytes)
		].join(' · ')
	);

	// The mono line says where the document went, or why there is nowhere yet.
	const detail = $derived(
		path
			? `→ ${path}`
			: status === 'discarded'
				? 'Discarded'
				: status === 'downloaded'
					? 'Downloaded to this browser'
					: 'Not exported yet'
	);
</script>

<a
	href={resolve('/session/[id]', { id: session.Id })}
	class="flex items-center gap-2.5 rounded-[14px] px-2.5 py-2 transition-colors hover:bg-accent-hover50"
	data-testid="session-row"
>
	<span
		class="flex h-10 w-8 flex-none items-center justify-center overflow-hidden rounded-[4px] bg-surface shadow-page"
	>
		{#if first}
			<img
				src={imageUrl(session.Id, first.Index, first.Hash, 'thumb')}
				alt=""
				loading="lazy"
				decoding="async"
				class="max-h-full max-w-full object-contain"
			/>
		{/if}
	</span>

	<span class="flex min-w-0 flex-1 flex-col gap-0.5">
		<span class="truncate text-sm font-bold">{sessionTitle(session)}</span>
		<span class="flex flex-wrap items-center gap-1.5">
			<SessionStatusPill {session} />
			<span class="text-xs text-ink-muted">{meta}</span>
		</span>
		<span class="truncate font-mono text-[11px] text-ink-faint">{detail}</span>
	</span>
</a>

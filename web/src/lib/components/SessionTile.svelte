<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { imageUrl } from '$lib/hub/hub';
	import { resolve } from '$app/paths';
	import { activePageCount, formatBytes, formatTime, sessionTitle } from '$lib/format';
	import SessionStatusPill from './SessionStatusPill.svelte';
	import SelectCheckbox from './SelectCheckbox.svelte';

	let {
		session,
		selectable = false,
		selected = false,
		ontoggle
	}: {
		session: SessionDto;
		selectable?: boolean;
		selected?: boolean;
		ontoggle?: (id: string) => void;
	} = $props();

	const first = $derived(session.Pages.find((p) => !p.Deleted && p.ThumbnailReady) ?? null);
	const pages = $derived(activePageCount(session));
	const meta = $derived(
		`${pages} p · ${formatBytes(session.SizeBytes)} · ${formatTime(session.CreatedAt)}`
	);
</script>

<svelte:element
	this={selectable ? 'button' : 'a'}
	type={selectable ? 'button' : undefined}
	href={selectable ? undefined : resolve('/session/[id]', { id: session.Id })}
	onclick={selectable ? () => ontoggle?.(session.Id) : undefined}
	role={selectable ? 'checkbox' : undefined}
	aria-checked={selectable ? selected : undefined}
	class="relative flex w-full flex-col gap-2 rounded-[16px] p-2.5 text-left transition-[transform,background-color] duration-250 ease-spring hover:-translate-y-[3px] {selected
		? 'bg-accent-50 shadow-ring-selected'
		: 'hover:bg-accent-hover50'}"
	data-testid="session-tile"
	data-selected={selected}
>
	{#if selectable}
		<span class="absolute top-4 left-4 z-1">
			<SelectCheckbox checked={selected} />
		</span>
	{/if}

	<span class="relative block pr-[5px] pb-[5px]">
		{#if pages > 1}
			<!-- A second sheet peeking out, so a multi-page scan reads as a stack. -->
			<span class="absolute inset-y-0 top-[5px] right-0 left-[5px] rounded-[6px] bg-accent-stack2"
			></span>
		{/if}
		<span
			class="relative flex w-full items-center justify-center overflow-hidden rounded-[6px] bg-surface shadow-page"
			style="aspect-ratio: 1 / 1.414"
			class:scale-[0.94]={selected}
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
			<span class="absolute right-1.5 bottom-1.5">
				<SessionStatusPill {session} size="thumb" />
			</span>
		</span>
	</span>

	<span class="flex flex-col gap-0.5">
		<span class="line-clamp-2 text-[13px] leading-[1.3] font-bold">{sessionTitle(session)}</span>
		<span class="text-[11px] text-ink-muted">{meta}</span>
	</span>
</svelte:element>

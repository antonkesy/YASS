<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { imageUrl } from '$lib/hub/hub';
	import { resolve } from '$app/paths';
	import {
		activePageCount,
		documentName,
		formatBytes,
		formatTime,
		lastEventPath,
		sessionStatus,
		sessionTitle
	} from '$lib/format';
	import SessionStatusPill from './SessionStatusPill.svelte';
	import SelectCheckbox from './SelectCheckbox.svelte';
	import { t } from '$lib/i18n/index.svelte';
	import { Icon } from '$lib/icons';

	let {
		session,
		template = '',
		selectable = false,
		selected = false,
		landed = false,
		ontoggle,
		ondownload,
		ondelete
	}: {
		session: SessionDto;
		template?: string;
		selectable?: boolean;
		selected?: boolean;
		landed?: boolean;
		ontoggle?: (id: string) => void;
		ondownload?: (id: string) => void;
		ondelete?: (id: string) => void;
	} = $props();

	const title = $derived(sessionTitle(session, template));
	const first = $derived(session.Pages.find((p) => !p.Deleted && p.ThumbnailReady) ?? null);
	const pages = $derived(activePageCount(session));
	const path = $derived(lastEventPath(session));
	const status = $derived(sessionStatus(session));

	const meta = $derived(
		[
			documentName(session, template) ? formatTime(session.CreatedAt) : null,
			t('home.pageCount', { n: pages }),
			formatBytes(session.SizeBytes)
		]
			.filter(Boolean)
			.join(' · ')
	);

	const detail = $derived(
		path
			? `→ ${path}`
			: status === 'discarded'
				? t('home.statusDiscarded')
				: status === 'downloaded'
					? t('home.downloadedToBrowser')
					: t('home.notDownloaded')
	);

	let confirmDelete = $state(false);
	function remove() {
		if (!confirmDelete) {
			confirmDelete = true;
			return;
		}
		confirmDelete = false;
		ondelete?.(session.Id);
	}

	const shell =
		'flex w-full items-center gap-2.5 rounded-[14px] px-2.5 py-2 text-left transition-colors';
</script>

<div
	class="group relative {selected ? 'rounded-[14px] bg-accent-50 shadow-ring-selected' : ''}"
	data-testid="session-row"
	data-selected={selected}
	class:animate-land-in={landed}
>
	<svelte:element
		this={selectable ? 'button' : 'a'}
		type={selectable ? 'button' : undefined}
		href={selectable ? undefined : resolve('/session/[id]', { id: session.Id })}
		class="{shell} {selected ? 'hover:bg-accent-wash2' : 'hover:bg-accent-hover50'}"
		onclick={selectable ? () => ontoggle?.(session.Id) : undefined}
		role={selectable ? 'checkbox' : undefined}
		aria-checked={selectable ? selected : undefined}
	>
		{#if selectable}
			<SelectCheckbox checked={selected} />
		{/if}

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
			<span class="truncate text-sm font-bold">{title}</span>
			<span class="flex flex-wrap items-center gap-1.5">
				<SessionStatusPill {session} />
				<span class="text-xs text-ink-muted">{meta}</span>
			</span>
			<span class="truncate font-mono text-[11px] text-ink-faint">{detail}</span>
		</span>
	</svelte:element>

	{#if !selectable && (ondownload || ondelete)}
		<span class="absolute inset-y-0 right-2.5 flex items-center gap-1">
			{#if ondownload}
				<button
					type="button"
					class="flex h-8 w-8 items-center justify-center rounded-full text-ink-muted transition-colors hover:bg-accent-wash hover:text-accent"
					onclick={() => ondownload(session.Id)}
					aria-label={t('home.downloadNamed', { title })}
					data-testid="row-download"
				>
					<Icon name="download" size={16} />
				</button>
			{/if}
			{#if ondelete}
				{#if confirmDelete}
					<button
						type="button"
						class="h-8 rounded-full bg-danger px-3 text-xs font-bold text-white transition-colors"
						onclick={remove}
						onblur={() => (confirmDelete = false)}
						aria-label={t('home.confirmDeleteNamed', { title })}
						data-testid="row-delete"
						{@attach (el) => el.focus()}
					>
						{t('home.confirmDelete')}
					</button>
				{:else}
					<button
						type="button"
						class="flex h-8 w-8 items-center justify-center rounded-full text-ink-muted transition-colors hover:bg-danger-bg hover:text-danger"
						onclick={remove}
						aria-label={t('home.deleteNamed', { title })}
						data-testid="row-delete"
					>
						<Icon name="trash" size={16} />
					</button>
				{/if}
			{/if}
		</span>
	{/if}
</div>

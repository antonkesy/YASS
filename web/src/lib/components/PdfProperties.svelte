<script lang="ts">
	import { slide } from 'svelte/transition';
	import { ExportFormat } from '$lib/hub/generated/Yass.Core.Hub';
	import type { MessageKey } from '$lib/i18n/index.svelte';
	import { t } from '$lib/i18n/index.svelte';
	import { Icon } from '$lib/icons';
	import { ms } from '$lib/motion';
	import {
		DOCUMENT_META_FIELDS,
		documentMeta,
		exportPrefs,
		metaFor,
		setDocumentMeta,
		type DocumentMeta
	} from '$lib/presets';

	let { sessionId }: { sessionId: string } = $props();

	/** What this scan saves with: Settings' export preferences, its own PDF properties on top. */
	const docPrefs = $derived(metaFor($exportPrefs, $documentMeta, sessionId));
	let open = $state(false);
	const META_LABELS: Record<keyof DocumentMeta, MessageKey> = {
		author: 'settings.author',
		subject: 'settings.subject',
		keywords: 'settings.keywords'
	};
	/** The properties as one line, for the closed disclosure. */
	const summary = $derived(
		DOCUMENT_META_FIELDS.map((f) => docPrefs[f].trim())
			.filter(Boolean)
			.join(' · ')
	);
</script>

{#if $exportPrefs.format === ExportFormat.Pdf}
	<!--
		This scan's own PDF properties, starting from the ones Settings gives every scan.
		Shut by default: a line saying what is set is all most saves need to see.
	-->
	<!-- The gap above is its own padding, so it opens and shuts with it. -->
	<div
		class="flex flex-col gap-2 pt-3"
		transition:slide={{ duration: ms(220) }}
		data-testid="pdf-properties"
	>
		<button
			type="button"
			class="flex items-center gap-2 rounded-[10px] text-left"
			aria-expanded={open}
			onclick={() => (open = !open)}
			data-testid="pdf-properties-toggle"
		>
			<span class="text-[13px] font-semibold text-ink-muted">{t('review.pdfProperties')}</span>
			<span class="ml-auto min-w-0 truncate text-[12px] text-ink-muted" title={summary}
				>{summary || t('review.pdfNone')}</span
			>
			<span
				class="flex-none text-ink-muted transition-transform {open ? 'rotate-90' : ''}"
				aria-hidden="true"><Icon name="chevronRight" /></span
			>
		</button>
		{#if open}
			<div
				class="flex flex-col gap-2"
				transition:slide={{ duration: ms(220) }}
				data-testid="pdf-fields"
			>
				{#each DOCUMENT_META_FIELDS as field (field)}
					<label class="flex flex-col gap-1">
						<span class="text-xs text-ink-muted">{t(META_LABELS[field])}</span>
						<input
							class="field h-9 px-2.5 text-[13px]"
							type="text"
							autocomplete="off"
							value={docPrefs[field]}
							oninput={(e) =>
								documentMeta.set(
									setDocumentMeta(
										$documentMeta,
										sessionId,
										$exportPrefs,
										field,
										e.currentTarget.value
									)
								)}
							data-testid="pdf-{field}"
						/>
					</label>
				{/each}
			</div>
		{/if}
	</div>
{/if}

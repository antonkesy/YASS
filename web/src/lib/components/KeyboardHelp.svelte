<script lang="ts">
	import { t } from '$lib/i18n/index.svelte';
	import Modal from './Modal.svelte';

	let { onclose }: { onclose: () => void } = $props();

	/**
	 * What each key does, grouped by where it works. Kept in step with the handlers by hand.
	 * Derived, so a change of language relabels it while it is open.
	 */
	const groups: { title: string; keys: [string[], string][] }[] = $derived([
		{
			title: t('dialogs.groupEverywhere'),
			keys: [
				[[t('dialogs.keyEsc')], t('dialogs.keyClose')],
				[['?'], t('dialogs.keyHelp')]
			]
		},
		{
			title: t('dialogs.groupReview'),
			keys: [
				[['←', '→'], t('dialogs.keyPrevNext')],
				[[t('dialogs.keyEnter')], t('dialogs.keyOpen')],
				[['R'], t('dialogs.keyRotateRight')],
				[[t('dialogs.keyShift'), 'R'], t('dialogs.keyRotateLeft')],
				[[t('dialogs.keyDel')], t('dialogs.keyDelete')],
				[['A'], t('dialogs.allPages')],
				[[t('dialogs.keyCtrl'), 'S'], t('dialogs.keySave')],
				[[t('dialogs.keyAlt'), '←'], t('dialogs.keyMove')]
			]
		},
		{
			title: t('dialogs.groupFullScreen'),
			keys: [
				[['←', '→'], t('dialogs.keyPrevNext')],
				[['Z'], t('dialogs.keyZoom')]
			]
		},
		{
			title: t('dialogs.allPages'),
			keys: [[['+', '−'], t('dialogs.keySize')]]
		}
	]);
</script>

<Modal
	title={t('dialogs.shortcutsTitle')}
	cancelLabel={t('dialogs.closeShortcuts')}
	oncancel={onclose}
	class="w-[min(560px,100%)] gap-4"
	top
	testid="keyboard-help"
>
	<div class="grid gap-5 sm:grid-cols-2">
		{#each groups as group (group.title)}
			<section class="flex flex-col gap-2">
				<h3 class="text-[12px] font-semibold text-ink-faint">
					{group.title}
				</h3>
				<dl class="flex flex-col gap-1.5">
					{#each group.keys as [keys, what] (what)}
						<div class="flex items-center justify-between gap-3">
							<dt class="text-[13px] text-ink-2">{what}</dt>
							<dd class="flex flex-none gap-1">
								{#each keys as key (key)}
									<kbd
										class="min-w-[26px] rounded-[7px] bg-ctl px-1.5 py-0.5 text-center font-mono text-[12px] font-bold text-ink shadow-[inset_0_-1.5px_0_rgb(0_0_0_/_0.12)]"
										>{key}</kbd
									>
								{/each}
							</dd>
						</div>
					{/each}
				</dl>
			</section>
		{/each}
	</div>
</Modal>

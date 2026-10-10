<script lang="ts">
	import { resolve } from '$app/paths';
	import { page as route } from '$app/state';
	import { getHub, sortedSessions } from '$lib/hub/hub';
	import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
	import StatusPill from './StatusPill.svelte';
	import Popover from './Popover.svelte';
	import { Icon } from '$lib/icons';
	import { cameFrom } from '$lib/navigation';
	import { ISSUE_TEMPLATES } from '$lib/links';
	import { i18n, LOCALE_NAMES, LOCALES, t } from '$lib/i18n/index.svelte';
	import { appearance } from '$lib/appearance.svelte';
	import { scale } from 'svelte/transition';
	import { ms } from '$lib/motion';

	let menu = $state<'language' | 'issue' | 'more' | null>(null);
	const close = () => (menu = null);

	// Icons only, each named by its aria-label and shown as a tooltip on hover.
	const button =
		'flex h-10 w-10 flex-none items-center justify-center rounded-full text-accent transition-colors hover:bg-ctl-hover active:opacity-60';
	const popup =
		'absolute top-[calc(100%+8px)] right-0 z-31 flex min-w-[200px] animate-pop-in flex-col rounded-[14px] bg-surface p-1.5 text-ink shadow-popover';
	const item =
		'flex min-h-[44px] w-full items-center rounded-[10px] px-3 text-left text-[13px] font-bold transition-colors hover:bg-ctl';

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

<!-- The phone's ⋯ menu repeats both lists, so they are written once. -->
{#snippet languages()}
	{#each LOCALES as locale (locale)}
		<button
			type="button"
			role="menuitemradio"
			aria-checked={i18n.locale === locale}
			lang={locale}
			class="{item} justify-between gap-3"
			onclick={() => {
				i18n.set(locale);
				close();
			}}
			data-testid="language-{locale}"
		>
			{LOCALE_NAMES[locale]}
			{#if i18n.locale === locale}<Icon name="checkSmall" />{/if}
		</button>
	{/each}
{/snippet}

{#snippet issues()}
	{#each ISSUE_TEMPLATES as template (template.id)}
		<a
			href={template.href}
			target="_blank"
			rel="noreferrer noopener external"
			role="menuitem"
			class={item}
			onclick={close}
			data-testid="issue-{template.id}"
		>
			{t(template.label)}
		</a>
	{/each}
{/snippet}

<!--
	Three columns rather than a flex row with a spacer, so whatever sits in the middle is
	centred on the viewport and not on the space left between the two ends.
-->
<header
	data-app-header
	class="sticky top-0 z-20 grid h-16 grid-cols-[1fr_auto_1fr] items-center gap-2.5 bg-header px-4 backdrop-blur-[20px] backdrop-saturate-[1.8] sm:px-7"
>
	<div class="flex min-w-0 items-center gap-2.5">
		{#if !atHome}
			<!-- Label and arrow are one link: the words are the obvious thing to click. -->
			<a
				href={resolve('/')}
				onclick={() => cameFrom.leaving(route.params.id ?? '')}
				aria-label={t('common.backHome')}
				title={t('common.backHome')}
				class="group flex items-center gap-2.5 rounded-[12px] text-[15px] font-bold text-ink-3 transition-colors hover:text-ink"
			>
				<span
					class="flex h-10 w-10 flex-none items-center justify-center rounded-full text-accent transition-colors group-hover:bg-ctl-hover group-active:opacity-60"
				>
					<Icon name="back" />
				</span>
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
			<!-- The name is the whole of the brand here. A phone has the buttons beside it to fit,
			     so there it goes by its initials. -->
			<span
				class="truncate text-[17px] font-bold tracking-[-0.02em] text-ink"
				data-testid="app-name"
			>
				<span class="sm:hidden" aria-hidden="true">YASS</span>
				<span class="max-sm:sr-only">{t('common.appName')}</span>
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
		Light or dark, language, documentation, issues: the app's own chrome, all icons. A phone
		has room for the switch and one more beside a centred title, so there the other three
		share a ⋯ menu, the way an iOS navigation bar does it.
	-->
	<div class="flex items-center justify-end gap-1.5">
		<!-- Shows the look it switches to: a moon while light, a sun while dark. -->
		<button
			type="button"
			class={button}
			aria-label={appearance.dark ? t('common.switchToLight') : t('common.switchToDark')}
			title={appearance.dark ? t('common.switchToLight') : t('common.switchToDark')}
			onclick={() => appearance.toggle()}
			data-testid="header-theme"
		>
			{#key appearance.dark}
				<span class="flex" in:scale={{ start: 0.6, duration: ms(220) }}>
					<Icon name={appearance.dark ? 'sun' : 'moon'} />
				</span>
			{/key}
		</button>

		<div class="relative max-sm:hidden">
			<button
				type="button"
				class="{button} gap-0.5 {menu === 'language' ? 'bg-ctl-hover' : ''}"
				aria-haspopup="menu"
				aria-expanded={menu === 'language'}
				aria-label={t('common.language')}
				title={t('common.language')}
				onclick={() => (menu = menu === 'language' ? null : 'language')}
				data-testid="header-language"
			>
				<Icon name="globe" />
			</button>
			{#if menu === 'language'}
				<Popover
					label={t('common.closeLanguageMenu')}
					onclose={close}
					class={popup}
					testid="language-menu"
				>
					{@render languages()}
				</Popover>
			{/if}
		</div>

		<a
			href={resolve('/docs')}
			class="{button} max-sm:hidden"
			aria-label={t('common.documentation')}
			title={t('common.documentation')}
			data-testid="header-docs"
		>
			<Icon name="book" />
		</a>

		<div class="relative max-sm:hidden">
			<button
				type="button"
				class="{button} {menu === 'issue' ? 'bg-ctl-hover' : ''}"
				aria-haspopup="menu"
				aria-expanded={menu === 'issue'}
				aria-label={t('common.reportIssue')}
				title={t('common.reportIssue')}
				onclick={() => (menu = menu === 'issue' ? null : 'issue')}
				data-testid="header-issue"
			>
				<Icon name="bug" />
			</button>
			{#if menu === 'issue'}
				<Popover
					label={t('common.closeIssueMenu')}
					onclose={close}
					class={popup}
					testid="issue-menu"
				>
					{@render issues()}
				</Popover>
			{/if}
		</div>

		<div class="relative sm:hidden">
			<button
				type="button"
				class="{button} {menu === 'more' ? 'bg-ctl-hover' : ''}"
				aria-haspopup="menu"
				aria-expanded={menu === 'more'}
				aria-label={t('common.more')}
				title={t('common.more')}
				onclick={() => (menu = menu === 'more' ? null : 'more')}
				data-testid="header-more"
			>
				<Icon name="more" />
			</button>
			{#if menu === 'more'}
				<Popover label={t('common.closeMoreMenu')} onclose={close} class={popup} testid="more-menu">
					{@render languages()}
					<div class="mx-3 my-1 h-px bg-line" role="separator"></div>
					<a
						href={resolve('/docs')}
						role="menuitem"
						class={item}
						onclick={close}
						data-testid="menu-docs"
					>
						{t('common.documentation')}
					</a>
					{@render issues()}
				</Popover>
			{/if}
		</div>
	</div>

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

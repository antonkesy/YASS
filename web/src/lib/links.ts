/** The project on GitHub, and where its issues are filed from the header's menu. */
export const REPO = 'https://github.com/antonkesy/YASS';

/**
 * The issue forms in .github/ISSUE_TEMPLATE, each opened straight on its own form. The
 * file names here have to match those files.
 */
export const ISSUE_TEMPLATES = (
	[
		{ id: 'bug', label: 'common.issueBug', file: 'bug_report.yml' },
		{
			id: 'feature',
			label: 'common.issueFeature',
			file: 'feature_request.yml'
		},
		{
			id: 'scanner',
			label: 'common.issueScanner',
			file: 'scanner_support.yml'
		}
	] as const
).map((t) => ({ ...t, href: `${REPO}/issues/new?template=${t.file}` }));

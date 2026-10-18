import type { Area } from '../types';

export const settings = {
	title: { en: 'Settings', de: 'Einstellungen', fr: 'Réglages', es: 'Ajustes' },
	pageTitle: {
		en: 'Settings · YASS',
		de: 'Einstellungen · YASS',
		fr: 'Réglages · YASS',
		es: 'Ajustes · YASS'
	},
	changedCount: {
		en: { one: '{n} changed', other: '{n} changed' },
		de: { one: '{n} geändert', other: '{n} geändert' },
		fr: { one: '{n} modifié', other: '{n} modifiés' },
		es: { one: '{n} cambiado', other: '{n} cambiados' }
	},
	changedTitle: {
		en: 'Scan options changed from the defaults for the next scan',
		de: 'Für den nächsten Scan geänderte Scan-Optionen',
		fr: 'Options modifiées par rapport aux valeurs par défaut pour le prochain scan',
		es: 'Opciones cambiadas respecto a las predeterminadas para el próximo escaneo'
	},
	reset: { en: 'Reset', de: 'Zurücksetzen', fr: 'Réinitialiser', es: 'Restablecer' },
	resetTitle: {
		en: 'Put every scan option back to its default',
		de: 'Alle Scan-Optionen auf Standard zurücksetzen',
		fr: 'Remettre toutes les options de scan par défaut',
		es: 'Devolver todas las opciones de escaneo a su valor predeterminado'
	},
	close: {
		en: 'Close settings',
		de: 'Einstellungen schließen',
		fr: 'Fermer les réglages',
		es: 'Cerrar los ajustes'
	},
	done: { en: 'Done', de: 'Fertig', fr: 'Terminé', es: 'Listo' },
	loading: { en: 'Loading', de: 'Wird geladen', fr: 'Chargement', es: 'Cargando' },

	scanner: { en: 'Scanner', de: 'Scanner', fr: 'Scanner', es: 'Escáner' },
	presets: { en: 'Presets', de: 'Profile', fr: 'Profils', es: 'Perfiles' },
	presetEdited: {
		en: '“{name}” edited',
		de: '„{name}“ geändert',
		fr: '« {name} » modifié',
		es: '“{name}” modificado'
	},
	custom: { en: 'Custom', de: 'Eigene', fr: 'Personnalisé', es: 'Personalizado' },
	presetEditedTitle: {
		en: 'Edited — press to go back to it',
		de: 'Geändert – tippe, um zurückzukehren',
		fr: 'Modifié — appuie pour y revenir',
		es: 'Modificado — pulsa para volver a él'
	},
	saveAsNew: {
		en: 'Save as new',
		de: 'Als neues speichern',
		fr: 'Enregistrer comme nouveau',
		es: 'Guardar como nuevo'
	},
	presetNamePlaceholder: {
		en: 'Preset name',
		de: 'Name des Profils',
		fr: 'Nom du profil',
		es: 'Nombre del perfil'
	},
	cancel: { en: 'Cancel', de: 'Abbrechen', fr: 'Annuler', es: 'Cancelar' },
	save: { en: 'Save', de: 'Speichern', fr: 'Enregistrer', es: 'Guardar' },
	icon: { en: 'Icon', de: 'Symbol', fr: 'Icône', es: 'Icono' },
	iconStar: { en: 'star', de: 'Stern', fr: 'étoile', es: 'estrella' },
	iconFile: { en: 'file', de: 'Datei', fr: 'fichier', es: 'archivo' },
	iconCopy: { en: 'copy', de: 'Kopie', fr: 'copie', es: 'copia' },
	iconReceipt: { en: 'receipt', de: 'Beleg', fr: 'reçu', es: 'recibo' },
	iconImage: { en: 'image', de: 'Bild', fr: 'image', es: 'imagen' },
	iconBriefcase: { en: 'briefcase', de: 'Aktentasche', fr: 'mallette', es: 'maletín' },
	iconHeart: { en: 'heart', de: 'Herz', fr: 'cœur', es: 'corazón' },
	iconHome: { en: 'home', de: 'Haus', fr: 'maison', es: 'casa' },
	iconBook: { en: 'book', de: 'Buch', fr: 'livre', es: 'libro' },
	iconMail: { en: 'mail', de: 'Brief', fr: 'courrier', es: 'correo' },
	iconTag: { en: 'tag', de: 'Etikett', fr: 'étiquette', es: 'etiqueta' },
	iconZap: { en: 'zap', de: 'Blitz', fr: 'éclair', es: 'rayo' },
	allScanners: {
		en: 'Available for all scanners',
		de: 'Für alle Scanner verfügbar',
		fr: 'Disponible pour tous les scanners',
		es: 'Disponible para todos los escáneres'
	},
	onlyOn: {
		en: '· otherwise only on {name}',
		de: '· sonst nur auf {name}',
		fr: '· sinon seulement sur {name}',
		es: '· si no, solo en {name}'
	},
	revert: { en: 'Revert', de: 'Verwerfen', fr: 'Rétablir', es: 'Revertir' },
	update: {
		en: 'Update “{name}”',
		de: '„{name}“ aktualisieren',
		fr: 'Mettre à jour « {name} »',
		es: 'Actualizar “{name}”'
	},
	delete: { en: 'Delete', de: 'Löschen', fr: 'Supprimer', es: 'Eliminar' },

	source: { en: 'Source', de: 'Quelle', fr: 'Source', es: 'Origen' },
	colourMode: { en: 'Colour mode', de: 'Farbmodus', fr: 'Mode couleur', es: 'Modo de color' },
	resolution: { en: 'Resolution', de: 'Auflösung', fr: 'Résolution', es: 'Resolución' },
	paperSize: {
		en: 'Paper size',
		de: 'Papierformat',
		fr: 'Format du papier',
		es: 'Tamaño de papel'
	},
	default: { en: 'Default', de: 'Standard', fr: 'Par défaut', es: 'Predeterminado' },
	defaultHint: {
		en: 'default {value}',
		de: 'Standard {value}',
		fr: 'par défaut {value}',
		es: 'predeterminado {value}'
	},
	defaultDpi: {
		en: 'default {dpi} dpi',
		de: 'Standard {dpi} dpi',
		fr: 'par défaut {dpi} dpi',
		es: 'predeterminado {dpi} ppp'
	},
	dpi: { en: 'dpi', de: 'dpi', fr: 'dpi', es: 'ppp' },
	flatbed: { en: 'Flatbed', de: 'Flachbett', fr: 'Vitre', es: 'Cristal' },
	feeder: { en: 'Feeder', de: 'Einzug', fr: 'Chargeur', es: 'Alimentador' },
	bothSides: { en: 'Both sides', de: 'Beidseitig', fr: 'Recto verso', es: 'Doble cara' },
	bw: { en: 'Black & white', de: 'Schwarzweiß', fr: 'Noir et blanc', es: 'Blanco y negro' },
	grayscale: { en: 'Grayscale', de: 'Graustufen', fr: 'Niveaux de gris', es: 'Escala de grises' },
	color: { en: 'Color', de: 'Farbe', fr: 'Couleur', es: 'Color' },
	letter: { en: 'Letter', de: 'Letter', fr: 'Lettre US', es: 'Carta' },
	legal: { en: 'Legal', de: 'Legal', fr: 'Légal US', es: 'Oficio' },
	cleanUp: { en: 'Clean-up', de: 'Bereinigung', fr: 'Nettoyage', es: 'Limpieza' },
	skipBlank: {
		en: 'Skip blank pages',
		de: 'Leere Seiten überspringen',
		fr: 'Ignorer les pages blanches',
		es: 'Omitir páginas en blanco'
	},
	blankThreshold: {
		en: 'Blank threshold',
		de: 'Leerseiten-Schwelle',
		fr: 'Seuil de page blanche',
		es: 'Umbral de página en blanco'
	},
	autoDeskew: {
		en: 'Auto-deskew',
		de: 'Automatisch begradigen',
		fr: 'Redressement auto',
		es: 'Enderezar automáticamente'
	},
	imageAdjustments: {
		en: 'Image adjustments',
		de: 'Bildanpassungen',
		fr: "Réglages de l'image",
		es: 'Ajustes de imagen'
	},
	brightness: { en: 'Brightness', de: 'Helligkeit', fr: 'Luminosité', es: 'Brillo' },
	contrast: { en: 'Contrast', de: 'Kontrast', fr: 'Contraste', es: 'Contraste' },
	resetLabel: {
		en: 'Reset {label}',
		de: '{label} zurücksetzen',
		fr: 'Réinitialiser {label}',
		es: 'Restablecer {label}'
	},

	devices: { en: 'Devices', de: 'Geräte', fr: 'Appareils', es: 'Dispositivos' },
	noScanners: {
		en: 'No scanner has been seen yet. Once the server finds one it is listed here, and stays listed while it is unplugged. If yours never shows up, the machine running YASS may need its driver: a SANE backend on Linux, the maker’s WIA driver on Windows.',
		de: 'Noch kein Scanner gefunden. Sobald der Server einen findet, steht er hier – auch wenn er ausgesteckt ist. Taucht deiner nie auf, braucht der Rechner, auf dem YASS läuft, vielleicht seinen Treiber: ein SANE-Backend unter Linux, den WIA-Treiber des Herstellers unter Windows.',
		fr: "Aucun scanner vu pour l'instant. Dès que le serveur en trouve un, il apparaît ici, même débranché. Si le vôtre n’apparaît jamais, la machine qui exécute YASS a peut-être besoin de son pilote : un backend SANE sous Linux, le pilote WIA du fabricant sous Windows.",
		es: 'Aún no se ha visto ningún escáner. Cuando el servidor encuentre uno, aparecerá aquí, incluso desconectado. Si el tuyo nunca aparece, puede que el equipo que ejecuta YASS necesite su controlador: un backend de SANE en Linux, el controlador WIA del fabricante en Windows.'
	},
	hidden: { en: 'Hidden', de: 'Ausgeblendet', fr: 'Masqué', es: 'Oculto' },
	connected: { en: 'Connected', de: 'Verbunden', fr: 'Connecté', es: 'Conectado' },
	notConnected: {
		en: 'Not connected',
		de: 'Nicht verbunden',
		fr: 'Non connecté',
		es: 'No conectado'
	},
	lastSeenAt: {
		en: 'last seen {time}',
		de: 'zuletzt gesehen {time}',
		fr: 'vu pour la dernière fois {time}',
		es: 'visto por última vez {time}'
	},
	rename: { en: 'Rename', de: 'Umbenennen', fr: 'Renommer', es: 'Renombrar' },
	renameLabel: {
		en: 'Rename {name}',
		de: '{name} umbenennen',
		fr: 'Renommer {name}',
		es: 'Renombrar {name}'
	},
	nameFor: {
		en: 'Name for {name}',
		de: 'Name für {name}',
		fr: 'Nom pour {name}',
		es: 'Nombre para {name}'
	},
	show: { en: 'Show', de: 'Einblenden', fr: 'Afficher', es: 'Mostrar' },
	hide: { en: 'Hide', de: 'Ausblenden', fr: 'Masquer', es: 'Ocultar' },
	chosenTitle: {
		en: 'This is the scanner the next scan runs on',
		de: 'Mit diesem Scanner läuft der nächste Scan',
		fr: 'C’est le scanner du prochain scan',
		es: 'Es el escáner del próximo escaneo'
	},
	showTitle: {
		en: 'Show it in the home screen’s scanner picker again',
		de: 'Wieder in der Scannerauswahl auf der Startseite zeigen',
		fr: 'Le réafficher dans le choix du scanner de l’accueil',
		es: 'Volver a mostrarlo en el selector de escáner del inicio'
	},
	hideTitle: {
		en: 'Leave it out of the home screen’s scanner picker',
		de: 'Nicht in der Scannerauswahl auf der Startseite zeigen',
		fr: 'Le retirer du choix du scanner de l’accueil',
		es: 'Quitarlo del selector de escáner del inicio'
	},
	detailsFor: {
		en: 'Details for {name}',
		de: 'Details zu {name}',
		fr: 'Détails de {name}',
		es: 'Detalles de {name}'
	},
	forget: {
		en: 'Forget this scanner',
		de: 'Scanner vergessen',
		fr: 'Oublier ce scanner',
		es: 'Olvidar este escáner'
	},
	factDriverName: {
		en: 'Driver name',
		de: 'Treibername',
		fr: 'Nom du pilote',
		es: 'Nombre del controlador'
	},
	factDriver: { en: 'Driver', de: 'Treiber', fr: 'Pilote', es: 'Controlador' },
	factAddress: { en: 'Address', de: 'Adresse', fr: 'Adresse', es: 'Dirección' },
	factDeviceId: {
		en: 'Device id',
		de: 'Geräte-ID',
		fr: "ID de l'appareil",
		es: 'ID del dispositivo'
	},
	factFirstSeen: {
		en: 'First seen here',
		de: 'Hier zuerst gesehen',
		fr: 'Vu ici la première fois',
		es: 'Visto aquí por primera vez'
	},
	factLastSeen: {
		en: 'Last seen',
		de: 'Zuletzt gesehen',
		fr: 'Vu en dernier',
		es: 'Visto por última vez'
	},
	factNow: { en: 'Now', de: 'Jetzt', fr: 'Maintenant', es: 'Ahora' },
	factScans: { en: 'Scans', de: 'Scans', fr: 'Scans', es: 'Escaneos' },
	factLastScan: { en: 'Last scan', de: 'Letzter Scan', fr: 'Dernier scan', es: 'Último escaneo' },

	export: { en: 'Export', de: 'Export', fr: 'Export', es: 'Exportar' },
	fileFormat: {
		en: 'File format',
		de: 'Dateiformat',
		fr: 'Format de fichier',
		es: 'Formato de archivo'
	},
	perDocument: {
		en: 'one file per document',
		de: 'eine Datei pro Dokument',
		fr: 'un fichier par document',
		es: 'un archivo por documento'
	},
	perPage: {
		en: 'one file per page',
		de: 'eine Datei pro Seite',
		fr: 'un fichier par page',
		es: 'un archivo por página'
	},
	serverSave: {
		en: 'Save to server',
		de: 'Auf dem Server speichern',
		fr: 'Enregistrer sur le serveur',
		es: 'Guardar en el servidor'
	},

	naming: { en: 'Naming', de: 'Benennung', fr: 'Nommage', es: 'Nombres' },
	useNameTemplate: {
		en: 'Name files from a template',
		de: 'Dateinamen aus einer Vorlage',
		fr: 'Nommer les fichiers avec un modèle',
		es: 'Nombrar archivos con una plantilla'
	},
	useNameTemplateOff: {
		en: 'Off: the save dialog starts with an empty name',
		de: 'Aus: Der Speichern-Dialog beginnt ohne Namen',
		fr: 'Désactivé : la boîte d’enregistrement s’ouvre sans nom',
		es: 'Desactivado: el diálogo de guardar empieza sin nombre'
	},
	fileNameTemplate: {
		en: 'File name template',
		de: 'Vorlage für Dateinamen',
		fr: 'Modèle de nom de fichier',
		es: 'Plantilla de nombre de archivo'
	},
	readyMade: { en: 'Ready-made', de: 'Fertige', fr: 'Tout prêts', es: 'Ya hechas' },
	closeReadyMade: {
		en: 'Close ready-made templates',
		de: 'Fertige Vorlagen schließen',
		fr: 'Fermer les modèles tout prêts',
		es: 'Cerrar las plantillas ya hechas'
	},
	unknownTokens: {
		en: 'Unknown: {tokens}',
		de: 'Unbekannt: {tokens}',
		fr: 'Inconnu : {tokens}',
		es: 'Desconocido: {tokens}'
	},

	metadata: { en: 'Metadata', de: 'Metadaten', fr: 'Métadonnées', es: 'Metadatos' },
	author: { en: 'Author', de: 'Autor', fr: 'Auteur', es: 'Autor' },
	authorPlaceholder: {
		en: 'Who the documents belong to',
		de: 'Wem die Dokumente gehören',
		fr: 'À qui sont les documents',
		es: 'De quién son los documentos'
	},
	subject: { en: 'Subject', de: 'Betreff', fr: 'Sujet', es: 'Asunto' },
	subjectPlaceholder: {
		en: 'What they are about',
		de: 'Worum es geht',
		fr: 'De quoi ils parlent',
		es: 'De qué tratan'
	},
	keywords: { en: 'Keywords', de: 'Stichwörter', fr: 'Mots-clés', es: 'Palabras clave' },
	keywordsPlaceholder: {
		en: 'invoices, tax, 2026',
		de: 'Rechnungen, Steuer, 2026',
		fr: 'factures, impôts, 2026',
		es: 'facturas, impuestos, 2026'
	},

	editing: { en: 'Editing', de: 'Bearbeiten', fr: 'Édition', es: 'Edición' },
	tileSize: {
		en: 'Page size in the all-pages view',
		de: 'Seitengröße in der Seitenübersicht',
		fr: 'Taille des pages dans la vue d’ensemble',
		es: 'Tamaño de página en la vista general'
	},
	tileFit: { en: 'Fit', de: 'Passend', fr: 'Ajusté', es: 'Ajustar' },
	tileTiny: { en: 'Tiny', de: 'Winzig', fr: 'Minuscule', es: 'Diminuto' },
	tileSmall: { en: 'Small', de: 'Klein', fr: 'Petit', es: 'Pequeño' },
	tileMedium: { en: 'Medium', de: 'Mittel', fr: 'Moyen', es: 'Mediano' },
	tileLarge: { en: 'Large', de: 'Groß', fr: 'Grand', es: 'Grande' },
	tileHuge: { en: 'Huge', de: 'Riesig', fr: 'Énorme', es: 'Enorme' },

	appearance: { en: 'Appearance', de: 'Darstellung', fr: 'Apparence', es: 'Apariencia' },
	appearanceHint: {
		en: 'Automatic follows this device',
		de: 'Automatisch folgt diesem Gerät',
		fr: 'Automatique suit cet appareil',
		es: 'Automático sigue a este dispositivo'
	},
	appearanceAuto: { en: 'Automatic', de: 'Automatisch', fr: 'Automatique', es: 'Automático' },
	appearanceLight: { en: 'Light', de: 'Hell', fr: 'Clair', es: 'Claro' },
	appearanceDark: { en: 'Dark', de: 'Dunkel', fr: 'Sombre', es: 'Oscuro' },

	pastScans: {
		en: 'Past scans',
		de: 'Frühere Scans',
		fr: 'Scans passés',
		es: 'Escaneos anteriores'
	},
	autoClear: {
		en: 'Delete old scans automatically',
		de: 'Alte Scans automatisch löschen',
		fr: 'Supprimer les anciens scans automatiquement',
		es: 'Eliminar los escaneos antiguos automáticamente'
	},
	olderThan: { en: 'Older than', de: 'Älter als', fr: 'Plus anciens que', es: 'Con más de' },
	minutes: { en: 'minutes', de: 'Minuten', fr: 'minutes', es: 'minutos' },
	hours: { en: 'hours', de: 'Stunden', fr: 'heures', es: 'horas' },
	days: { en: 'days', de: 'Tage', fr: 'jours', es: 'días' },
	months: { en: 'months', de: 'Monate', fr: 'mois', es: 'meses' }
} satisfies Area;

# Déclare la prise en charge des paramètres avancés.
[CmdletBinding()]
param (
    # Définit le paramètre -Path. Il n'est pas obligatoire.
    # S'il est absent, sa valeur par défaut sera le répertoire courant.
    [Parameter(Mandatory=$false, ValueFromPipeline=$true)]
    [string]$Path = (Get-Location).Path,

    # Définit le paramètre pour le format de fichier (ex: *.cs, *.txt, *.py).
    # Il n'est pas obligatoire et sa valeur par défaut est '*.cs'.
    [Parameter(Mandatory=$false)]
    [string]$FileFormat = "*.cs"
)

# Construit le chemin complet pour le fichier de sortie.
# Le nom du fichier de sortie a été rendu plus générique.
$outputFile = Join-Path -Path $Path -ChildPath "combined_source_code.txt"

# Affiche le répertoire et le filtre qui seront analysés.
Write-Host "Analyse du répertoire : $Path"
Write-Host "Filtre de fichier : $FileFormat"

# Supprime le fichier de sortie s'il existe déjà pour garantir un résultat propre.
if (Test-Path $outputFile) {
    Remove-Item $outputFile
}

# Récupère tous les fichiers correspondant au filtre de manière récursive.
# Le bloc try/catch gère les erreurs si le chemin n'est pas valide.
try {
    # Utilise le paramètre $FileFormat dans le filtre.
    $foundFiles = Get-ChildItem -Path $Path -Filter $FileFormat -Recurse -ErrorAction Stop

    # Vérifie si des fichiers ont été trouvés avant de continuer.
    if ($foundFiles.Count -eq 0) {
        Write-Warning "Aucun fichier trouvé avec le filtre '$FileFormat' dans le chemin '$Path'."
        return
    }

    # Boucle sur chaque fichier trouvé.
    foreach ($file in $foundFiles) {
        # Affiche le fichier en cours de traitement.
        Write-Host "Ajout de : $($file.FullName)"

        # Crée la ligne de commentaire avec le chemin complet du fichier.
        $header = "// $($file.FullName)"

        # Ajoute d'abord la ligne de commentaire, puis le contenu complet du fichier
        # au fichier de sortie. -Raw lit le fichier en une seule chaîne, ce qui est plus efficace.
        Add-Content -Path $outputFile -Value $header
        Add-Content -Path $outputFile -Value (Get-Content -Path $file.FullName -Raw)
        Add-Content -Path $outputFile -Value "`n" # Ajoute un saut de ligne pour la lisibilité
    }

    Write-Host "`n✅ Terminé ! Le fichier combiné est ici : $outputFile"
}
catch {
    # Affiche une erreur si le chemin n'existe pas ou n'est pas accessible.
    Write-Error "❌ Une erreur est survenue. Vérifiez que le chemin '$Path' est valide."
}
extends SceneTree

const DEFAULT_MOD_ID := "Sts2PathHelper"

func _initialize() -> void:
    var args := OS.get_cmdline_user_args()
    if args.is_empty():
        push_error("Missing output .pck path.")
        quit(1)
        return

    var output_path := args[0]
    var manifest := _load_manifest()
    var mod_id := _resolve_mod_id(manifest)
    var pack_root := _resolve_pack_root(mod_id)
    var packer := PCKPacker.new()
    var err := packer.pck_start(output_path)
    if err != OK:
        push_error("Failed to start pck build: %s" % err)
        quit(err)
        return

    var files := {}
    if pack_root != "":
        _collect_files(ProjectSettings.globalize_path(pack_root), "res://%s" % mod_id, files)
        _collect_imported_dependencies(files)

    for target_path in files.keys():
        var source_path: String = files[target_path]
        err = packer.add_file(target_path, source_path)
        if err != OK:
            push_error("Failed to add %s -> %s (%s)" % [source_path, target_path, err])
            quit(err)
            return

    err = packer.flush()
    if err != OK:
        push_error("Failed to finalize pck: %s" % err)
        quit(err)
        return

    print("Built pck: %s" % output_path)
    quit(0)

func _load_manifest() -> Dictionary:
    var manifest_path := ProjectSettings.globalize_path("res://mod_manifest.json")
    if not FileAccess.file_exists(manifest_path):
        return {}

    var json := FileAccess.get_file_as_string(manifest_path)
    var parsed: Variant = JSON.parse_string(json)
    if parsed is Dictionary:
        return parsed

    return {}

func _resolve_mod_id(manifest: Dictionary) -> String:
    var mod_id := String(manifest.get("id", DEFAULT_MOD_ID)).strip_edges()
    if mod_id == "":
        return DEFAULT_MOD_ID
    return mod_id

func _resolve_pack_root(mod_id: String) -> String:
    var preferred := "res://pack_assets/%s" % mod_id
    if DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(preferred)):
        return preferred

    if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path("res://pack_assets")):
        return ""

    var dir := DirAccess.open("res://pack_assets")
    if dir == null:
        return ""

    dir.list_dir_begin()
    while true:
        var name := dir.get_next()
        if name == "":
            break
        if name.begins_with("."):
            continue
        if dir.current_is_dir():
            dir.list_dir_end()
            return "res://pack_assets/%s" % name
    dir.list_dir_end()
    return ""

func _collect_files(source_dir: String, target_dir: String, files: Dictionary) -> void:
    var dir := DirAccess.open(source_dir)
    if dir == null:
        push_error("Missing pack source dir: %s" % source_dir)
        quit(2)
        return

    dir.list_dir_begin()
    while true:
        var name := dir.get_next()
        if name == "":
            break
        if name.begins_with("."):
            continue
        if name.begins_with("cover_source."):
            continue

        var source_path := source_dir.path_join(name)
        var target_path := target_dir.path_join(name)
        if dir.current_is_dir():
            _collect_files(source_path, target_path, files)
        else:
            files[target_path] = source_path
    dir.list_dir_end()

func _collect_imported_dependencies(files: Dictionary) -> void:
    var target_paths := files.keys()
    for target_path_variant in target_paths:
        var target_path := String(target_path_variant)
        if not target_path.ends_with(".import"):
            continue

        var import_source_path := String(files[target_path])
        var config := ConfigFile.new()
        var err := config.load(import_source_path)
        if err != OK:
            push_error("Failed to read import metadata %s (%s)" % [import_source_path, err])
            quit(err)
            return

        var dest_files_variant: Variant = config.get_value("deps", "dest_files", [])
        if dest_files_variant is not Array:
            continue

        for dest_file_variant in dest_files_variant:
            var dest_file := String(dest_file_variant)
            if dest_file == "":
                continue

            var source_path := ProjectSettings.globalize_path(dest_file)
            if not FileAccess.file_exists(dest_file):
                push_error("Missing imported resource %s referenced by %s. Run Godot import before packing." % [dest_file, import_source_path])
                quit(3)
                return

            files[dest_file] = source_path

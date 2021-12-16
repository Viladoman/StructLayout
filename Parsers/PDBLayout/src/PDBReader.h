#pragma once

namespace PDBReader
{
	bool ExportAtLocation(const wchar_t* pdbFile, const wchar_t* filename, const int line, const wchar_t* output);
}
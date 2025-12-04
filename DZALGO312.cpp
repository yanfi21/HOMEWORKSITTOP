#include <iostream>
using namespace std;


void printMatrix(int** matrix, int rows, int cols) {
    for (int i = 0; i < rows; ++i) {
        for (int j = 0; j < cols; ++j) {
            cout << matrix[i][j] << " ";
        }
        cout << endl;
    }
    cout << endl;
}

int** createMatrix(int rows, int cols) {
    int** matrix = new int* [rows];
    for (int i = 0; i < rows; ++i) {
        matrix[i] = new int[cols];
    }
    return matrix;
}

void fillMatrix(int** matrix) {
    int count = 10;
    for (int i = 0; i < 4; ++i) {
        for (int j = 0; j < 5; ++j) {
            matrix[i][j] = count++;
        }
    }
}

int main() {
    setlocale(LC_ALL, "Russian");
    int rows = 4, cols = 5;
    int** matrix = createMatrix(rows, cols);
    fillMatrix(matrix);
    cout << "Матрица 4х5 :" << endl;
    printMatrix(matrix, rows, cols);
    return 0;
}
